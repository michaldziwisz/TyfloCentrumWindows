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
$projectPath = Join-Path $repoRoot 'src\TyfloCentrum.Windows.App\TyfloCentrum.Windows.App.csproj'
$packageRoot = Join-Path $repoRoot 'artifacts\SignedAppPackages'
$certificateRoot = Join-Path $repoRoot 'artifacts\DevCertificate'
$publisher = 'CN=TyfloCentrum'
$certificateName = 'TyfloCentrum.Dev.Package'
$certificatePassword = 'Tyflo1234'
$codeSigningEku = '1.3.6.1.5.5.7.3.3'
$packageSigningEku = '1.3.6.1.4.1.311.84.3.1'

New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Force -Path $certificateRoot | Out-Null

$cerPath = Join-Path $certificateRoot "$certificateName.cer"

$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $publisher -and
        $_.HasPrivateKey -and
        ($_.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq $codeSigningEku }) -and
        ($_.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq $packageSigningEku })
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
            "2.5.29.37={text}$codeSigningEku,$packageSigningEku",
            '2.5.29.19={text}'
        )
}

Remove-Item -Path $cerPath -Force -ErrorAction SilentlyContinue

Export-Certificate -Cert $certificate -FilePath $cerPath -Type CERT -Force | Out-Null

if (-not (Test-Path $cerPath)) {
    throw "Nie udalo sie wyeksportowac pliku CER: $cerPath"
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
    /p:PackageCertificateThumbprint=$($certificate.Thumbprint)

if ($LASTEXITCODE -ne 0) {
    throw "MSBuild zakonczyl sie bledem: $LASTEXITCODE"
}

$latestPackage = Get-ChildItem $packageRoot -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $latestPackage) {
    throw 'Nie znaleziono wygenerowanego katalogu AppPackages.'
}

$latestCer = Get-ChildItem $latestPackage.FullName -Filter '*.cer' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $latestCer) {
    $latestMsix = Get-ChildItem $latestPackage.FullName -Filter '*.msix' |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $latestMsix) {
        throw "Nie znaleziono pliku MSIX w katalogu: $($latestPackage.FullName)"
    }

    $targetCerPath = Join-Path $latestPackage.FullName ("{0}.cer" -f [System.IO.Path]::GetFileNameWithoutExtension($latestMsix.Name))
    Copy-Item -Path $cerPath -Destination $targetCerPath -Force
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
