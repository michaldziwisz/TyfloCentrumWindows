param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',
    [switch]$Install
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path
$projectPath = Join-Path $repoRoot 'src\Tyflocentrum.Windows.App\Tyflocentrum.Windows.App.csproj'
$packageRoot = Join-Path $repoRoot 'artifacts\SignedAppPackages'
$certificateRoot = Join-Path $repoRoot 'artifacts\DevCertificate'
$publisher = 'CN=Tyflocentrum'
$certificateName = 'Tyflocentrum.Dev.Package'
$certificatePassword = 'Tyflo1234'
$codeSigningEku = '1.3.6.1.5.5.7.3.3'

New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Force -Path $certificateRoot | Out-Null

$pfxPath = Join-Path $certificateRoot "$certificateName.pfx"
$cerPath = Join-Path $certificateRoot "$certificateName.cer"

$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $publisher -and
        $_.HasPrivateKey -and
        ($_.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq $codeSigningEku })
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $publisher `
        -FriendlyName 'TyfloCentrum Dev Package Signing' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -KeyExportPolicy Exportable `
        -HashAlgorithm SHA256 `
        -KeyUsage DigitalSignature `
        -NotAfter (Get-Date).AddYears(5) `
        -TextExtension @(
            "2.5.29.37={text}$codeSigningEku",
            '2.5.29.19={text}'
        )
}

$securePassword = ConvertTo-SecureString -String $certificatePassword -Force -AsPlainText

Remove-Item -Path $pfxPath -Force -ErrorAction SilentlyContinue
Remove-Item -Path $cerPath -Force -ErrorAction SilentlyContinue

Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $securePassword -Force | Out-Null
Export-Certificate -Cert $certificate -FilePath $cerPath -Type CERT -Force | Out-Null

if (-not (Test-Path $pfxPath)) {
    throw "Nie udalo sie wyeksportowac pliku PFX: $pfxPath"
}

& 'C:\Program Files\dotnet\dotnet.exe' msbuild `
    $projectPath `
    /restore `
    /t:Publish `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /p:GenerateAppxPackageOnBuild=true `
    /p:AppxPackageDir="$packageRoot\" `
    /p:UapAppxPackageBuildMode=SideloadOnly `
    /p:AppxBundle=Never `
    /p:AppxPackageSigningEnabled=true `
    /p:PackageCertificateKeyFile=$pfxPath `
    /p:PackageCertificatePassword=$certificatePassword

if ($LASTEXITCODE -ne 0) {
    throw "MSBuild zakonczyl sie bledem: $LASTEXITCODE"
}

$latestPackage = Get-ChildItem $packageRoot -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $latestPackage) {
    throw 'Nie znaleziono wygenerowanego katalogu AppPackages.'
}

Write-Host ''
Write-Host 'Gotowy pakiet developerski:' -ForegroundColor Green
Write-Host $latestPackage.FullName
Write-Host ''

if ($Install) {
    Write-Host 'Uruchamiam reinstalacje pakietu testowego...' -ForegroundColor Yellow
    & (Join-Path $repoRoot 'scripts\windows\Install-DevMsix.ps1') -PackageDirectory $latestPackage.FullName
}
else {
    Write-Host 'Nastepny krok:' -ForegroundColor Yellow
    Write-Host "powershell -ExecutionPolicy Bypass -File `"$repoRoot\scripts\windows\Install-DevMsix.ps1`""
    Write-Host ''
    Write-Host 'Albo jednym poleceniem:' -ForegroundColor Yellow
    Write-Host "powershell -ExecutionPolicy Bypass -File `"$repoRoot\scripts\windows\Build-DevMsix.ps1`" -Install"
}
