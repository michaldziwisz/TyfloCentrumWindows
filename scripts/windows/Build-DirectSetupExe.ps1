param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path
$packageRoot = Join-Path $repoRoot 'artifacts\SignedAppPackages'
$outputRoot = Join-Path $repoRoot 'artifacts\DirectSetup'
$iexpressPath = Join-Path $env:WINDIR 'System32\iexpress.exe'

if (-not (Test-Path $iexpressPath)) {
    throw "Nie znaleziono IExpress: $iexpressPath"
}

& (Join-Path $scriptRoot 'Build-DevMsix.ps1') -Configuration $Configuration -Platform $Platform

$packageDirectory = Get-ChildItem $packageRoot -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $packageDirectory) {
    throw 'Nie znaleziono katalogu z pakietem MSIX.'
}

$msix = Get-ChildItem $packageDirectory.FullName -Filter *.msix |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $msix) {
    throw 'Nie znaleziono pliku MSIX do zapakowania w setup EXE.'
}

$setupDirectory = Join-Path $outputRoot ("TyfloCentrumSetup_{0}_{1}" -f $Configuration, $Platform)
$payloadDirectory = Join-Path $setupDirectory 'payload'
$outputExe = Join-Path $setupDirectory ("TyfloCentrumSetup_{0}.exe" -f $Platform)
$installCommandFile = Join-Path $payloadDirectory 'Install-TyfloCentrum.cmd'
$payloadArchivePath = Join-Path $payloadDirectory 'TyfloCentrumPayload.zip'
$sedPath = Join-Path $setupDirectory 'TyfloCentrumSetup.sed'

Remove-Item -Path $setupDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $payloadDirectory | Out-Null

$installCommand = @'
@echo off
setlocal
set "SETUP_LOG=%TEMP%\TyfloCentrumSetup.log"
set "PAYLOAD_ZIP=%~dp0TyfloCentrumPayload.zip"
set "EXTRACT_DIR=%TEMP%\TyfloCentrumSetup_%RANDOM%%RANDOM%"

echo ==== %DATE% %TIME% ====>>"%SETUP_LOG%"
echo Start setup bootstrap.>>"%SETUP_LOG%"
echo Payload: %PAYLOAD_ZIP%>>"%SETUP_LOG%"
echo Extract dir: %EXTRACT_DIR%>>"%SETUP_LOG%"

mkdir "%EXTRACT_DIR%" >nul 2>&1
if errorlevel 1 goto extract_failed

echo Extracting payload...>>"%SETUP_LOG%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -LiteralPath '%PAYLOAD_ZIP%' -DestinationPath '%EXTRACT_DIR%' -Force"
if errorlevel 1 goto extract_failed

echo Starting Install.ps1...>>"%SETUP_LOG%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%EXTRACT_DIR%\Install.ps1" -Force
set "EXIT_CODE=%ERRORLEVEL%"
echo Install.ps1 exit code: %EXIT_CODE%>>"%SETUP_LOG%"

if "%EXIT_CODE%"=="0" (
    echo Installation finished successfully.>>"%SETUP_LOG%"
    rmdir /s /q "%EXTRACT_DIR%" >nul 2>&1
    exit /b 0
)

echo.
echo Instalacja TyfloCentrum zakonczyla sie bledem. Kod: %EXIT_CODE%
echo Rozpakowane pliki zostaly tutaj:
echo %EXTRACT_DIR%
echo Installation failed. Files left in: %EXTRACT_DIR%>>"%SETUP_LOG%"
echo See log: %SETUP_LOG%>>"%SETUP_LOG%"
pause
exit /b %EXIT_CODE%

:extract_failed
echo.
echo Nie udalo sie przygotowac instalatora TyfloCentrum.
echo Sprobuj uruchomic plik ponownie.
echo Extract failed.>>"%SETUP_LOG%"
pause
exit /b 1
'@
Set-Content -Path $installCommandFile -Value $installCommand -Encoding ASCII

Compress-Archive -Path (Join-Path $packageDirectory.FullName '*') -DestinationPath $payloadArchivePath -Force

$payloadFiles = Get-ChildItem $payloadDirectory -File | Sort-Object Name

$stringsLines = New-Object System.Collections.Generic.List[string]
$sourceSectionLines = New-Object System.Collections.Generic.List[string]
$sourceFilesSectionLines = New-Object System.Collections.Generic.List[string]

$stringsLines.Add('InstallPrompt=')
$stringsLines.Add('DisplayLicense=')
$stringsLines.Add('FinishMessage=')
$stringsLines.Add("TargetName=$outputExe")
$stringsLines.Add('FriendlyName=Instalator TyfloCentrum')
$stringsLines.Add('AppLaunched=Install-TyfloCentrum.cmd')
$stringsLines.Add('PostInstallCmd=<None>')
$stringsLines.Add('AdminQuietInstCmd=Install-TyfloCentrum.cmd')
$stringsLines.Add('UserQuietInstCmd=Install-TyfloCentrum.cmd')

$sourceSectionLines.Add('[SourceFiles]')
$sourceSectionLines.Add("SourceFiles0=$payloadDirectory\\")
$sourceFilesSectionLines.Add('[SourceFiles0]')

for ($i = 0; $i -lt $payloadFiles.Count; $i++) {
    $variableName = "FILE{0}" -f $i
    $stringsLines.Add($variableName + '="' + $payloadFiles[$i].Name + '"')
    $sourceFilesSectionLines.Add('%' + $variableName + '%=')
}

$sedContent = @(
    '[Version]'
    'Class=IEXPRESS'
    'SEDVersion=3'
    '[Options]'
    'PackagePurpose=InstallApp'
    'ShowInstallProgramWindow=1'
    'HideExtractAnimation=1'
    'UseLongFileName=1'
    'InsideCompressed=0'
    'CAB_FixedSize=0'
    'CAB_ResvCodeSigning=0'
    'RebootMode=N'
    'InstallPrompt=%InstallPrompt%'
    'DisplayLicense=%DisplayLicense%'
    'FinishMessage=%FinishMessage%'
    'TargetName=%TargetName%'
    'FriendlyName=%FriendlyName%'
    'AppLaunched=%AppLaunched%'
    'PostInstallCmd=%PostInstallCmd%'
    'AdminQuietInstCmd=%AdminQuietInstCmd%'
    'UserQuietInstCmd=%UserQuietInstCmd%'
    'SourceFiles=SourceFiles'
    '[Strings]'
) + $stringsLines + $sourceSectionLines + $sourceFilesSectionLines

Set-Content -Path $sedPath -Value $sedContent -Encoding ASCII

Push-Location $setupDirectory
try {
    & $iexpressPath /N $sedPath | Out-Null
}
finally {
    Pop-Location
}

if (-not (Test-Path $outputExe)) {
    throw "IExpress nie wygenerowal instalatora EXE: $outputExe"
}

Write-Host ''
Write-Host 'Gotowy instalator direct EXE:' -ForegroundColor Green
Write-Host $outputExe
Write-Host ''
Write-Host 'Payload instalatora:' -ForegroundColor Green
Write-Host $payloadDirectory
Write-Host ''
Write-Host 'Uwagi:' -ForegroundColor Yellow
Write-Host '- Instalator EXE rozpakowuje pelny payload MSIX do katalogu tymczasowego i uruchamia Install.ps1.'
Write-Host '- To jest kanal poza Microsoft Store.'
Write-Host '- Uzytkownik uruchamia pojedynczy plik EXE.'
