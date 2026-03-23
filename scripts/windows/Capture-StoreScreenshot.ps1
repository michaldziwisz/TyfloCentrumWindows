param(
    [string]$OutputPath = '',
    [string]$Section = 'news',
    [int]$TimeoutSeconds = 45
)

$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Get-DefaultOutputPath {
    $repoRoot = Get-RepoRoot
    return Join-Path $repoRoot 'store\assets\screenshots\pl-PL\01-nowosci.png'
}

function Get-AppExecutablePath {
    $package = Get-AppxPackage | Where-Object {
        $_.Name -eq 'MichaDziwisz.TyfloCentrum' -or
        $_.PackageFamilyName -like 'MichaDziwisz.TyfloCentrum_*'
    } | Select-Object -First 1

    if ($null -ne $package) {
        $exePath = Join-Path $package.InstallLocation 'TyfloCentrum.Windows.App.exe'
        if (Test-Path $exePath) {
            return $exePath
        }
    }

    $repoRoot = Get-RepoRoot
    $localBuildCandidates = @(
        (Join-Path $repoRoot 'src\TyfloCentrum.Windows.App\bin\x64\Debug\net8.0-windows10.0.19041.0\TyfloCentrum.Windows.App.exe'),
        (Join-Path $repoRoot 'src\TyfloCentrum.Windows.App\bin\x64\Release\net8.0-windows10.0.19041.0\TyfloCentrum.Windows.App.exe')
    )

    foreach ($candidate in $localBuildCandidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw 'Nie znaleziono ani zainstalowanej paczki, ani lokalnej binarki aplikacji.'
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Get-DefaultOutputPath
}

$outputDirectory = Split-Path -Parent $OutputPath
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Force
}

$exePath = Get-AppExecutablePath
$arguments = @(
    '--internal-store-screenshot', $Section,
    '--internal-store-screenshot-output', $OutputPath
)

$process = Start-Process -FilePath $exePath -ArgumentList $arguments -PassThru

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $OutputPath) {
        Write-Output $OutputPath
        exit 0
    }

    if ($process.HasExited) {
        break
    }

    Start-Sleep -Milliseconds 300
}

if (Test-Path $OutputPath) {
    Write-Output $OutputPath
    exit 0
}

throw 'Nie udalo sie wygenerowac screenshotu Store w oczekiwanym czasie.'
