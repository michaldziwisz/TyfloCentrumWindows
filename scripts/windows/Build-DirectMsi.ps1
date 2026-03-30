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
$installerProjectPath = Join-Path $repoRoot 'installer\TyfloCentrum.Windows.Installer\TyfloCentrum.Windows.Installer.wixproj'
$publishDirectory = Join-Path $repoRoot ("artifacts\Unpackaged\publish-{0}" -f $Platform.ToLowerInvariant())
$outputDirectory = Join-Path $repoRoot ("artifacts\DirectMsi\TyfloCentrumMsi_{0}_{1}" -f $Configuration, $Platform)

$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    throw "Nie znaleziono dotnet.exe: $dotnet"
}

$manifestPath = Join-Path $repoRoot 'src\TyfloCentrum.Windows.App\Package.appxmanifest'
[xml]$manifest = Get-Content -Path $manifestPath -Encoding UTF8
$manifestVersion = $manifest.Package.Identity.Version
if ([string]::IsNullOrWhiteSpace($manifestVersion)) {
    throw "Nie znaleziono wersji pakietu w: $manifestPath"
}

$versionParts = $manifestVersion.Split('.')
if ($versionParts.Length -lt 3) {
    throw "Nieprawidłowa wersja pakietu: $manifestVersion"
}

$msiVersion = '{0}.{1}.{2}' -f $versionParts[0], $versionParts[1], $versionParts[2]

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

$installerOutputPath = Join-Path $outputDirectory 'bin\'
& $dotnet build $installerProjectPath `
    -c $Configuration `
    -p:Platform=$Platform `
    -p:PublishDir="$publishDirectory\" `
    -p:ProductVersion=$msiVersion `
    -p:OutputPath=$installerOutputPath `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Build MSI nie powiodl sie. Kod: $LASTEXITCODE"
}

$msi = Get-ChildItem -Path $installerOutputPath -Filter *.msi -Recurse |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $msi) {
    throw 'Nie znaleziono wygenerowanego instalatora MSI.'
}

$finalMsiPath = Join-Path $outputDirectory ("TyfloCentrumSetup_{0}_{1}.msi" -f $msiVersion, $Platform)
Copy-Item -Path $msi.FullName -Destination $finalMsiPath -Force

Write-Host ''
Write-Host 'Gotowy instalator MSI:' -ForegroundColor Green
Write-Host $finalMsiPath
Write-Host ''
Write-Host 'Publish directory:' -ForegroundColor Green
Write-Host $publishDirectory
