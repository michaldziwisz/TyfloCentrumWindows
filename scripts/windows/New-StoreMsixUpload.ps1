[CmdletBinding()]
param(
    [string]$PackageDirectory,
    [switch]$OpenFolder
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$defaultPackagesRoot = Join-Path $repoRoot 'artifacts\SignedAppPackages'

if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $latestPackage = Get-ChildItem $defaultPackagesRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $latestPackage) {
        throw 'Nie znaleziono katalogu AppPackages. Najpierw zbuduj release MSIX.'
    }

    $PackageDirectory = $latestPackage.FullName
}

$resolvedPackageDirectory = (Resolve-Path $PackageDirectory).Path

$msix = Get-ChildItem $resolvedPackageDirectory -Filter '*.msix' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $msix) {
    throw "Nie znaleziono pliku MSIX w katalogu: $resolvedPackageDirectory"
}

$appxsym = Get-ChildItem $resolvedPackageDirectory -Filter '*.appxsym' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

$msixuploadPath = Join-Path $resolvedPackageDirectory (
    '{0}.msixupload' -f [System.IO.Path]::GetFileNameWithoutExtension($msix.Name)
)
$temporaryZipPath = Join-Path $resolvedPackageDirectory (
    '{0}.zip' -f [System.IO.Path]::GetFileNameWithoutExtension($msix.Name)
)

$archiveEntries = @($msix.Name)
if ($appxsym) {
    $archiveEntries += $appxsym.Name
}

Push-Location $resolvedPackageDirectory
try {
    if (Test-Path $temporaryZipPath) {
        Remove-Item $temporaryZipPath -Force
    }

    Compress-Archive -Path $archiveEntries -DestinationPath $temporaryZipPath -Force
    Move-Item -Path $temporaryZipPath -Destination $msixuploadPath -Force
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host 'Gotowy pakiet do Microsoft Store:' -ForegroundColor Green
Write-Host $msixuploadPath
Write-Host ''
Write-Host 'Zawartosc archiwum:' -ForegroundColor Green
$archiveEntries | ForEach-Object { Write-Host $_ }
Write-Host ''
Write-Host 'Uwaga:' -ForegroundColor Yellow
Write-Host 'Do .msixupload trafia tylko .msix oraz opcjonalnie .appxsym. Nie dolaczaj pliku .cer.'

if ($OpenFolder) {
    Start-Process explorer.exe $resolvedPackageDirectory
}
