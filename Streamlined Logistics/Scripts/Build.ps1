<#
.SYNOPSIS
Builds Streamlined Logistics, a Kiln mod, and installs it as a Kiln drop-in.

.DESCRIPTION
Streamlined Logistics shows what the linked storage chests hold in one list, from anywhere in a world the player hosts, and moves items
between them and the backpack through Kiln's item service.

The script runs the chest rule tests (tests\ChestRulesTests), builds src\ against Kiln's assemblies in <game>\kiln\host, and puts the
mod folder in dist\streamlined-logistics (kiln.mod.json, StreamlinedLogistics.dll and its pdb).

With -Install it copies that folder to <game>\kiln\mods\streamlined-logistics, where Kiln loads it as a drop-in (the profile allows Kiln
drop-ins). The game must be closed.
#>
[CmdletBinding()]
param(
    # Also copy the mod into the game's Kiln drop-in folder (game closed).
    [switch]$Install,
    [string]$GamePath = 'D:\SteamLibrary\steamapps\common\Enshrouded'
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $projectRoot 'src'
$manifestPath = Join-Path $sourceRoot 'kiln.mod.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$modRoot = Join-Path $projectRoot "dist\$($manifest.id)"
$kilnHost = Join-Path $GamePath 'kiln\host'
if (-not (Test-Path (Join-Path $kilnHost 'Kiln.Api.dll'))) { throw "Kiln's assemblies not found in $kilnHost." }

dotnet run -c Release --project (Join-Path $projectRoot 'tests\ChestRulesTests')
if ($LASTEXITCODE -ne 0) { throw 'The chest rule tests failed.' }

$buildRoot = Join-Path $projectRoot 'build'
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
    # The mod was called Remote Chests up to 0.7.1: its drop-in goes, or Kiln would load both. Its settings and data stay, as a backup;
    # the mod copies them over at its first start.
    $formerDropinRoot = Join-Path $GamePath 'kiln\mods\remote-chests'
    if (Test-Path $formerDropinRoot) {
        Remove-Item $formerDropinRoot -Recurse -Force
        Write-Host "Removed the former Remote Chests drop-in ($formerDropinRoot)"
    }
    # Up to 0.9.1 a native companion lit the chests; Kiln outlines them itself since 0.10.0.
    foreach ($formerGlow in (Join-Path $GamePath 'greeneye\streamlined-logistics-glow'), (Join-Path $GamePath 'greeneye\remote-chests-glow')) {
        if (Test-Path $formerGlow) {
            Remove-Item $formerGlow -Recurse -Force
            Write-Host "Removed the former glow companion ($formerGlow)"
        }
    }
}
