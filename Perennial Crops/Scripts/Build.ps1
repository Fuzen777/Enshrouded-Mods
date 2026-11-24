<#
.SYNOPSIS
Builds Perennial Crops, a Kiln mod, and installs it as a Kiln drop-in.

.DESCRIPTION
Perennial Crops makes every planted crop keep producing after harvest, as the game's own banana, coconut, coffee, date palm and hazelnut do.
It changes the game's templates before the game reads them (Kiln resource patches staged in OnBoot), so it needs Kiln's boot snapshot of the
game's data, which Kiln's launcher makes at Play.

The script runs the planner tests (tests\CropPlannerTests, on that snapshot), builds src\ against Kiln's assemblies in <game>\kiln\host, and puts
the mod folder in dist\perennial-crops (kiln.mod.json, PerennialCrops.dll and its pdb).

With -Install it copies that folder to <game>\kiln\mods\perennial-crops, where Kiln loads it as a drop-in (the profile allows Kiln drop-ins).
The game must be closed.
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

dotnet run -c Release --project (Join-Path $projectRoot 'tests\CropPlannerTests') -- $GamePath
if ($LASTEXITCODE -ne 0) { throw 'The crop planner tests failed.' }

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
}
