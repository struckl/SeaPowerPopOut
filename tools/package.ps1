<#
.SYNOPSIS
    Builds a release of Sea Power: R.A.T.S. Pop-Out Windows and stages the folder that goes to the Steam Workshop.

.DESCRIPTION
    Release build without the automatic deploy, then dist\SeaPowerPopOut\ with exactly what players get:
    SeaPowerPopOut.dll (one DLL for both game versions), _info.ini, lang\*.ini, LICENSE.txt, CHANGELOG.md and preview.jpg - the
    Workshop preview (workshop\preview.jpg, if present), which the game's upload screen only lets you pick from inside StreamingAssets.
    The DLL is checked against both games' assemblies with the workspace's tools\compat-check: nothing missing on either.
    The globe game's assemblies are -GlobeManagedDir when the installed game isn't the globe version; the 0.8.7 ones
    -FlatManagedDir (default: _gamerefs in the workspace).
    With -Install the staged folder also replaces <GameDir>\Sea Power_Data\StreamingAssets\SeaPowerPopOut\, the folder to pick in
    the game's Create Mod screen. Sea Power must be closed for that. -Zip also writes dist\SeaPowerPopOut-<version>.zip.
    The steps are the shared ones of every R.A.T.S. mod (SeaPowerCore\tools\RatsPackage.psm1); the workspace is found
    above this repo, -Workspace names it for a clone elsewhere, -RatsCore another copy of the Core repo.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Install
#>
param(
    [string]$GameDir = $(if ($env:GameDir) { $env:GameDir } else { 'E:\Games\steamapps\common\Sea Power' }),
    [string]$Workspace = $env:RatsWorkspace,
    [string]$RatsCore = $env:RatsCore,
    [string]$FlatManagedDir = $env:FlatManagedDir,
    # The globe game's assemblies when the installed game isn't the globe version (a saved copy of its Managed).
    [string]$GlobeManagedDir = $env:GlobeManagedDir,
    [switch]$Install,
    [switch]$Zip
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Workspace) {
    $Workspace = $root
    while ($Workspace -and -not (Test-Path (Join-Path $Workspace 'mods.json'))) { $Workspace = Split-Path -Parent $Workspace }
    if (-not $Workspace) { throw 'No workspace (a folder with mods.json) above this repo - pass -Workspace.' }
}
if (-not $RatsCore) { $RatsCore = Join-Path $Workspace 'SeaPowerCore' }
Import-Module (Join-Path $RatsCore 'tools\RatsPackage.psm1') -Force

$name = 'SeaPowerPopOut'
$project = Join-Path $root "src\$name\$name.csproj"
$dist = Join-Path $root "dist\$name"
if (-not $FlatManagedDir) { $FlatManagedDir = Join-Path $Workspace '_gamerefs\SeaPower-0.8.7-public\Managed' }
if (-not $GlobeManagedDir) { $GlobeManagedDir = Join-Path $GameDir 'Sea Power_Data\Managed' }

$env:RatsCore = $RatsCore.TrimEnd('\') + '\'      # the csproj's -p:RatsCore
Build-RatsMod -Project $project -Dist $dist -GameDir $GameDir -GlobeManagedDir $GlobeManagedDir -FlatManagedDir $FlatManagedDir -Workspace $Workspace -SingleBuild | Out-Null
Test-RatsCompat -Workspace $Workspace -Dist $dist -RootNamespace $name -GlobeManagedDir $GlobeManagedDir -FlatManagedDir $FlatManagedDir -SingleBuild
Copy-RatsCommon -Root $root -Dist $dist -InfoIni (Join-Path $root "src\$name\mod\_info.ini")
# The window-handling texts in the game's nine languages, read from next to the DLL.
Copy-Item -Recurse (Join-Path $root "src\$name\mod\lang") (Join-Path $dist 'lang')
$version = Get-RatsModVersion -PluginFile (Join-Path $root "src\$name\Plugin.cs")
Write-Host "Staged Sea Power: R.A.T.S. Pop-Out Windows $version in $dist"
Show-RatsStage -Dist $dist
if ($Zip) { New-RatsZip -Dist $dist -Version $version }
if ($Install) { Install-RatsMod -Dist $dist -GameDir $GameDir }
