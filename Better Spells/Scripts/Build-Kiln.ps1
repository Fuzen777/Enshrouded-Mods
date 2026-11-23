<#
.SYNOPSIS
Builds Better Spells, a Kiln mod, and installs it as a Kiln drop-in.

.DESCRIPTION
Better Spells (src\better-spells-kiln, id better-spells) changes spells through Kiln's game data service while playing. Its settings are
in the Kiln menu and in the kiln config better-spells.json.

The script runs the tests (tests\SpellRulesTests: the steps and calculations, the settings, and every name pattern against the game data
dump when -DumpPath exists), builds the mod against Kiln's assemblies in <game>\kiln\host, and puts the mod folder in
dist\kiln\better-spells (kiln.mod.json, BetterSpells.dll and its pdb).

With -Install it copies that folder to <game>\kiln\mods\better-spells, where Kiln loads it as a drop-in (the profile allows Kiln
drop-ins). The game must be closed. The settings are not touched: they are in Kiln's profile, not in the mod's folder.

The two Shroudforge mods this one replaces, and their Build.ps1, are in the repository's history (tags better-spells/v2.0.9 and
better-spells-live/v1.0.5).
#>
[CmdletBinding()]
param(
    # Also copy the mod into the game's Kiln drop-in folder (game closed).
    [switch]$Install,
    [string]$GamePath = 'D:\SteamLibrary\steamapps\common\Enshrouded',
    # The offline game data dump of the game's build; the name patterns are checked against it when it exists.
    [string]$DumpPath = 'D:\Dev\Enshrouded\Dump\26430625'
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $projectRoot 'src\better-spells-kiln'
$manifestPath = Join-Path $sourceRoot 'kiln.mod.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$modRoot = Join-Path $projectRoot "dist\kiln\$($manifest.id)"
$kilnHost = Join-Path $GamePath 'kiln\host'
if (-not (Test-Path (Join-Path $kilnHost 'Kiln.Content.dll'))) { throw "Kiln's assemblies not found in $kilnHost." }

$testArguments = @('run', '-c', 'Release', "-p:KilnHost=$kilnHost", '--project', (Join-Path $projectRoot 'tests\SpellRulesTests'))
if (Test-Path $DumpPath) { $testArguments += @('--', $DumpPath) } else { Write-Warning "No game data dump in ${DumpPath}: the name patterns are not checked." }
dotnet @testArguments
if ($LASTEXITCODE -ne 0) { throw 'The tests failed.' }

$buildRoot = Join-Path $projectRoot 'build\better-spells-kiln'
dotnet build $sourceRoot -c Release -nologo -v q "-p:Version=$($manifest.version)" "-p:KilnHost=$kilnHost" "-p:OutDir=$buildRoot\"
if ($LASTEXITCODE -ne 0) { throw 'The build failed.' }

if (Test-Path $modRoot) { Remove-Item $modRoot -Recurse -Force }
New-Item -ItemType Directory -Force $modRoot | Out-Null
Copy-Item $manifestPath $modRoot
foreach ($name in "$($manifest.entryAssembly)", [IO.Path]::ChangeExtension($manifest.entryAssembly, '.pdb')) {
    Copy-Item (Join-Path $buildRoot $name) $modRoot
}
Write-Host "Built $modRoot ($($manifest.name) $($manifest.version))"

if ($Install) {
    if (Get-Process enshrouded -ErrorAction SilentlyContinue) { throw 'The game is running: close it first, Kiln has the mod loaded.' }
    $dropinRoot = Join-Path $GamePath "kiln\mods\$($manifest.id)"
    if (Test-Path $dropinRoot) { Remove-Item $dropinRoot -Recurse -Force }
    Copy-Item $modRoot $dropinRoot -Recurse
    Write-Host "Installed in $dropinRoot"
}
