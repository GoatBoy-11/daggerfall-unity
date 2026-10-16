# Temporarily installs a bundle, runs the focused in-game suite, then restores the installed bundle and settings.
param(
    [Parameter(Mandatory=$true)][string]$Bundle,
    [string]$Dfu = 'F:\_Projects\Dagerfall\DFU_testing',
    [string]$Character = 'Testo',
    [string]$Save = 'AtDaggerfall',
    [string]$OutputLog = 'F:\_Projects\Dagerfall\output\ai-packages-selftest.log'
)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name DaggerfallUnity -ErrorAction SilentlyContinue) { throw 'Close DFU before running the package test.' }
$bundlePath = (Resolve-Path -LiteralPath $Bundle).Path
$installed = Join-Path $Dfu 'DaggerfallUnity_Data\StreamingAssets\Mods\advancednpcs.dfmod'
$flag = Join-Path $Dfu 'DaggerfallUnity_Data\StreamingAssets\ANPCs\selftest-autorun.txt'
$data = Join-Path $env:USERPROFILE 'AppData\LocalLow\Daggerfall Workshop\Daggerfall Unity'
$ini = Join-Path $data 'settings.ini'
$log = Join-Path $data 'Player.log'
if (Test-Path -LiteralPath $flag) { throw 'An existing self-test autorun file is present; leave it untouched.' }
$oldBundle = [IO.File]::ReadAllBytes($installed)
$oldSettings = [IO.File]::ReadAllBytes($ini)
$taskGame = $null
try {
    Copy-Item -LiteralPath $bundlePath -Destination $installed -Force
    $settings = [IO.File]::ReadAllText($ini)
    $settings = $settings -replace '(?m)^ShowOptionsAtStart = True', 'ShowOptionsAtStart = False'
    $settings = $settings -replace '(?m)^Fullscreen = True', 'Fullscreen = False'
    $settings = $settings -replace '(?m)^ResolutionWidth = .*', 'ResolutionWidth = 960'
    $settings = $settings -replace '(?m)^ResolutionHeight = .*', 'ResolutionHeight = 540'
    $settings = $settings -replace '(?m)^RunInBackground = False', 'RunInBackground = True'
    [IO.File]::WriteAllText($ini, $settings)
    [IO.File]::WriteAllText($flag, "$Character`n$Save`npackages`n")
    $taskGame = Start-Process -FilePath (Join-Path $Dfu 'DaggerfallUnity.exe') -WorkingDirectory $Dfu -WindowStyle Hidden -PassThru
    if (!$taskGame.WaitForExit(180000)) {
        Stop-Process -Id $taskGame.Id -Force
        $taskGame.WaitForExit()
        throw 'Package self-test timed out after 3 minutes.'
    }
    Copy-Item -LiteralPath $log -Destination $OutputLog -Force
    $text = [IO.File]::ReadAllText($OutputLog)
    $text -split "`n" | Where-Object { $_ -match '\[AdvancedNPCs\] SELFTEST|Exception' } | Write-Output
    if ($text -notmatch 'SELFTEST DONE' -or $text -match 'SELFTEST FAIL') { throw "Package self-test failed; see $OutputLog" }
} finally {
    if ($taskGame -and !$taskGame.HasExited) { Stop-Process -Id $taskGame.Id -Force; $taskGame.WaitForExit() }
    [IO.File]::WriteAllBytes($installed, $oldBundle)
    [IO.File]::WriteAllBytes($ini, $oldSettings)
    if (Test-Path -LiteralPath $flag) { Remove-Item -LiteralPath $flag -Force }
}
