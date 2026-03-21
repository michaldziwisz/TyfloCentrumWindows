param(
    [string]$PackageDirectory
)

$ErrorActionPreference = 'Stop'

function Get-PackageIdentityName([string]$repoRoot) {
    $manifestPath = Join-Path $repoRoot 'src\Tyflocentrum.Windows.App\Package.appxmanifest'
    [xml]$manifest = Get-Content $manifestPath
    return $manifest.Package.Identity.Name
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-LatestPackageDirectory([string]$packageRoot) {
    return Get-ChildItem $packageRoot -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path
$packageRoot = Join-Path $repoRoot 'artifacts\SignedAppPackages'
$packageIdentityName = Get-PackageIdentityName $repoRoot

if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $latest = Get-LatestPackageDirectory $packageRoot
    if (-not $latest) {
        throw 'Nie znaleziono katalogu z pakietem. Najpierw uruchom Build-DevMsix.ps1.'
    }

    $PackageDirectory = $latest.FullName
}

$installScript = Join-Path $PackageDirectory 'Install.ps1'
if (-not (Test-Path $installScript)) {
    throw "Nie znaleziono Install.ps1 w katalogu: $PackageDirectory"
}

function Remove-ExistingPackage([string[]]$packageNames) {
    Get-Process -Name 'Tyflocentrum.Windows.App' -ErrorAction SilentlyContinue | Stop-Process -Force

    foreach ($packageName in $packageNames | Select-Object -Unique) {
        $installedPackages = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
        foreach ($package in $installedPackages) {
            Write-Host "Usuwam poprzednia wersje: $($package.PackageFullName)" -ForegroundColor Yellow
            Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
        }
    }
}

if (-not (Test-IsAdministrator)) {
    Write-Host 'Uruchamiam reinstalacje pakietu z podniesionymi uprawnieniami...' -ForegroundColor Yellow
    Start-Process `
        -FilePath 'powershell.exe' `
        -Verb RunAs `
        -ArgumentList @(
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-File', "`"$PSCommandPath`"",
            '-PackageDirectory', "`"$PackageDirectory`""
        ) | Out-Null
    exit 0
}

Remove-ExistingPackage -packageNames @($packageIdentityName, 'Tyflocentrum.Windows')

Write-Host 'Instaluje najnowszy pakiet testowy...' -ForegroundColor Green
& $installScript -Force
