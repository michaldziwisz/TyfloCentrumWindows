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
$sedPath = Join-Path $setupDirectory 'TyfloCentrumSetup.sed'

Remove-Item -Path $setupDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $payloadDirectory | Out-Null

Copy-Item -Path (Join-Path $packageDirectory.FullName '*') -Destination $payloadDirectory -Recurse -Force

$installCommand = @'
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1" -Force
exit /b %ERRORLEVEL%
'@
Set-Content -Path $installCommandFile -Value $installCommand -Encoding ASCII

$payloadFiles = Get-ChildItem $payloadDirectory -File | Sort-Object Name

$stringsLines = New-Object System.Collections.Generic.List[string]
$sourceSectionLines = New-Object System.Collections.Generic.List[string]
$sourceFilesSectionLines = New-Object System.Collections.Generic.List[string]

$stringsLines.Add('InstallPrompt=')
$stringsLines.Add('DisplayLicense=')
$stringsLines.Add('FinishMessage=')
$stringsLines.Add("TargetName=$outputExe")
$stringsLines.Add('FriendlyName=Instalator TyfloCentrum')
$stringsLines.Add('AppLaunched=cmd.exe /c Install-TyfloCentrum.cmd')
$stringsLines.Add('PostInstallCmd=<None>')
$stringsLines.Add('AdminQuietInstCmd=cmd.exe /c Install-TyfloCentrum.cmd')
$stringsLines.Add('UserQuietInstCmd=cmd.exe /c Install-TyfloCentrum.cmd')

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
Write-Host '- Instalator EXE rozpakowuje dzialajacy pakiet MSIX i uruchamia Install.ps1.'
Write-Host '- To jest kanal poza Microsoft Store.'
Write-Host '- Uzytkownik uruchamia pojedynczy plik EXE.'
