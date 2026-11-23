<#
.SYNOPSIS
Builds the Terrain Tool Radius DLL, runs its tests and packs the Vortex zip.

.DESCRIPTION
Compiles src\native\terrain_tool_radius.cpp into TerrainToolRadius.dll and tests\test_main.cpp into a test program with
Visual Studio's C++ tools (found through vswhere). The test program checks the settings parser, that the manifest's
settings match the DLL's, and that both hook sites are found exactly once in the installed enshrouded.exe; it also writes
the code caves, which tests\check_caves.py disassembles and checks instruction by instruction (py with capstone).

Then it assembles build\terrain-tool-radius from src\terrain-tool-radius and the DLL, and zips that folder into dist.
Vortex installs a zip whose top level is <id>\mod.json into the game's mods folder.
#>
[CmdletBinding()]
param(
    [string]$GameFolder = 'D:\SteamLibrary\steamapps\common\Enshrouded'
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
$wrapperRoot = Join-Path $projectRoot 'src\terrain-tool-radius'
$nativeRoot = Join-Path $projectRoot 'src\native'
$testsRoot = Join-Path $projectRoot 'tests'
$buildRoot = Join-Path $projectRoot 'build'
$objectRoot = Join-Path $buildRoot 'obj'
$testBuildRoot = Join-Path $buildRoot 'tests'
$modBuildRoot = Join-Path $buildRoot 'terrain-tool-radius'
$distRoot = Join-Path $projectRoot 'dist'
$gameExecutablePath = Join-Path $GameFolder 'enshrouded.exe'

$modManifest = Get-Content (Join-Path $wrapperRoot 'mod.json') -Raw | ConvertFrom-Json
$zipPath = Join-Path $distRoot ("{0} {1}.zip" -f $modManifest.name, $modManifest.version)

$nativeSource = Get-Content (Join-Path $nativeRoot 'terrain_tool_radius.cpp') -Raw
if ($nativeSource -notmatch 'kModVersion = "([^"]+)"' -or $Matches[1] -ne $modManifest.version) {
    throw "kModVersion in terrain_tool_radius.cpp isn't mod.json's version $($modManifest.version)."
}
if (-not (Test-Path $gameExecutablePath)) { throw "enshrouded.exe not found in $GameFolder (pass -GameFolder)." }

$vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$visualStudioPath = & $vswherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $visualStudioPath) { throw 'No Visual Studio with the C++ x64 tools found.' }
$vcvarsPath = Join-Path $visualStudioPath 'VC\Auxiliary\Build\vcvars64.bat'

if (Test-Path $buildRoot) { Remove-Item $buildRoot -Recurse -Force }
New-Item -ItemType Directory -Force $objectRoot, $testBuildRoot, $modBuildRoot, $distRoot | Out-Null

$compilerOptions = '/nologo /std:c++20 /O2 /MT /EHsc /W4 /WX /permissive- /utf-8'
$dllPath = Join-Path $modBuildRoot 'TerrainToolRadius.dll'
$testProgramPath = Join-Path $testBuildRoot 'test_main.exe'
# vcvars64.bat calls vswhere by name, so its folder goes on PATH first.
$compileScriptPath = Join-Path $buildRoot 'compile.cmd'
@(
    '@echo off'
    "set `"PATH=$(Split-Path $vswherePath);%PATH%`""
    "call `"$vcvarsPath`" >nul || exit /b 1"
    "cl $compilerOptions /LD /Fo`"$objectRoot\\`" `"$nativeRoot\terrain_tool_radius.cpp`" /Fe`"$dllPath`" /link /NOLOGO /IMPLIB:`"$objectRoot\TerrainToolRadius.lib`" || exit /b 1"
    "cl $compilerOptions /Fo`"$objectRoot\\`" `"$testsRoot\test_main.cpp`" /Fe`"$testProgramPath`" || exit /b 1"
) | Set-Content $compileScriptPath -Encoding ascii

& $compileScriptPath
if ($LASTEXITCODE -ne 0) { throw "Compiling failed (exit code $LASTEXITCODE)." }

$cavesPath = Join-Path $testBuildRoot 'caves.bin'
& $testProgramPath $gameExecutablePath (Join-Path $wrapperRoot 'extended.mod.json') $cavesPath
if ($LASTEXITCODE -ne 0) { throw "test_main.exe failed (exit code $LASTEXITCODE)." }
& py (Join-Path $testsRoot 'check_caves.py') $cavesPath
if ($LASTEXITCODE -ne 0) { throw "check_caves.py failed (exit code $LASTEXITCODE)." }

Copy-Item (Join-Path $wrapperRoot '*') $modBuildRoot -Recurse
Get-ChildItem $modBuildRoot -File | Where-Object { $_.Extension -in '.exp', '.lib' } | Remove-Item

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
[IO.Compression.ZipFile]::CreateFromDirectory($modBuildRoot, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $true)
Write-Host "Built $zipPath"
