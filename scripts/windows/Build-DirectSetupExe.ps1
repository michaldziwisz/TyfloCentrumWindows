param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path
$appProjectPath = Join-Path $repoRoot 'src\TyfloCentrum.Windows.App\TyfloCentrum.Windows.App.csproj'
$innoScriptPath = Join-Path $repoRoot 'installer\TyfloCentrum.Windows.Installer\TyfloCentrum.DirectSetup.iss'
$publishDirectory = Join-Path $repoRoot ("artifacts\Unpackaged\publish-{0}" -f $Platform.ToLowerInvariant())
$outputDirectory = Join-Path $repoRoot ("artifacts\DirectSetup\TyfloCentrumSetup_{0}_{1}" -f $Configuration, $Platform)
$installerIconPath = Join-Path $repoRoot 'src\TyfloCentrum.Windows.App\Assets\AppIcon.ico'

$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    throw "Nie znaleziono dotnet.exe: $dotnet"
}

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)

$isccPath = $null
foreach ($candidate in $isccCandidates) {
    if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate)) {
        $isccPath = $candidate
        break
    }
}
if (-not $isccPath) {
    throw 'Nie znaleziono ISCC.exe. Zainstaluj Inno Setup 6, aby zbudowac klasyczny instalator EXE.'
}

$manifestPath = Join-Path $repoRoot 'src\TyfloCentrum.Windows.App\Package.appxmanifest'
[xml]$manifest = Get-Content -Path $manifestPath -Encoding UTF8
$manifestVersion = $manifest.Package.Identity.Version
if ([string]::IsNullOrWhiteSpace($manifestVersion)) {
    throw "Nie znaleziono wersji pakietu w: $manifestPath"
}

$versionParts = $manifestVersion.Split('.')
if ($versionParts.Length -lt 3) {
    throw "Nieprawidlowa wersja pakietu: $manifestVersion"
}

$installerVersion = '{0}.{1}.{2}' -f $versionParts[0], $versionParts[1], $versionParts[2]
$outputBaseFileName = "TyfloCentrumSetup_{0}_{1}" -f $installerVersion, $Platform

Remove-Item -Path $outputDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

& $dotnet publish $appProjectPath `
    -c $Configuration `
    -r ("win-" + $Platform.ToLowerInvariant()) `
    -o $publishDirectory `
    -p:Platform=$Platform `
    -p:TyfloCentrumDistributionMode=Unpackaged `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Publish unpackaged nie powiodl sie. Kod: $LASTEXITCODE"
}

if (-not (Test-Path (Join-Path $publishDirectory 'TyfloCentrum.Windows.App.exe'))) {
    throw "Publish nie wygenerowal TyfloCentrum.Windows.App.exe w: $publishDirectory"
}

& $isccPath `
    "/DMyAppVersion=$manifestVersion" `
    "/DMyInstallerVersion=$installerVersion" `
    "/DMyPlatform=$Platform" `
    "/DMyPublishDir=$publishDirectory" `
    "/DMyOutputDir=$outputDirectory" `
    "/DMyOutputBaseFilename=$outputBaseFileName" `
    "/DMySetupIconFile=$installerIconPath" `
    $innoScriptPath

if ($LASTEXITCODE -ne 0) {
    throw "Build instalatora EXE nie powiodl sie. Kod: $LASTEXITCODE"
}

$setupExe = Get-ChildItem -Path $outputDirectory -Filter *.exe -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $setupExe) {
    throw 'Nie znaleziono wygenerowanego instalatora EXE.'
}

Write-Host ''
Write-Host 'Gotowy instalator EXE:' -ForegroundColor Green
Write-Host $setupExe.FullName
Write-Host ''
Write-Host 'Publish directory:' -ForegroundColor Green
Write-Host $publishDirectory
