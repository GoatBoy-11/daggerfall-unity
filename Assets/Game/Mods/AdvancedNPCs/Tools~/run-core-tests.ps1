# Runs the same Core NUnit suite without an Editor license, using Unity's bundled NUnit and Mono.
param(
    [string]$OutputDirectory = 'F:\_Projects\Dagerfall\output\ai-packages-core-tests',
    [string]$TestHost = 'F:\_Projects\Dagerfall\anpc_testhost',
    [string]$MonoRoot = 'C:\Program Files\Unity\Hub\Editor\2019.4.41f2\Editor\Data\MonoBleedingEdge'
)
$ErrorActionPreference = 'Stop'
$modRoot = Split-Path $PSScriptRoot -Parent
$nunitDll = Get-ChildItem -LiteralPath (Join-Path $TestHost 'Library\PackageCache') -Filter 'nunit.framework.dll' -Recurse |
    Where-Object { $_.FullName -match 'net472' } | Select-Object -First 1
if (!$nunitDll) { throw 'No cached NUnit framework found in the test host.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Copy-Item -LiteralPath $nunitDll.FullName -Destination (Join-Path $OutputDirectory 'nunit.framework.dll') -Force
$exe = Join-Path $OutputDirectory 'CoreTests.exe'
$sources = @((Get-ChildItem -LiteralPath (Join-Path $modRoot 'Scripts\Core') -Filter '*.cs').FullName)
$sources += @((Get-ChildItem -LiteralPath (Join-Path $modRoot 'Editor\Tests') -Filter '*.cs').FullName)
$sources += Join-Path $PSScriptRoot 'CoreTestHarness.cs'
$compileArgs = @('-nologo', '-langversion:4', '-target:exe', ('-out:' + $exe), ('-r:' + $nunitDll.FullName))
& (Join-Path $MonoRoot 'bin\mono.exe') (Join-Path $MonoRoot 'lib\mono\4.5\mcs.exe') @compileArgs @sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& (Join-Path $MonoRoot 'bin\mono.exe') $exe (Join-Path $OutputDirectory 'results.xml')
exit $LASTEXITCODE
