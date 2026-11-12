<#
.SYNOPSIS
    Links this repository into the game's LocalMods folder and reports the state.

.DESCRIPTION
    Barotrauma loads local content packages from
        <game>/LocalMods/<name>/filelist.xml
    Instead of copying files on every edit, this creates a directory junction so
    the game always sees the live repository. Run with -Remove to unlink.

.EXAMPLE
    pwsh tools/deploy.ps1
    pwsh tools/deploy.ps1 -Remove
#>
[CmdletBinding()]
param(
    [string]$GameDir = 'D:\Steam\steamapps\common\Barotrauma',
    [string]$ModName = 'GlobalOverhaul',
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$localMods = Join-Path $GameDir 'LocalMods'
$link = Join-Path $localMods $ModName

if ($Remove) {
    if (Test-Path $link) { (Get-Item $link).Delete(); Write-Host "removed $link" }
    else { Write-Host "nothing to remove at $link" }
    return
}

if (-not (Test-Path $localMods)) { throw "LocalMods folder not found: $localMods" }

if (Test-Path $link) {
    $item = Get-Item $link -Force
    if ($item.LinkType -eq 'Junction' -and $item.Target -eq $repoRoot) {
        Write-Host "already linked: $link -> $repoRoot"
        return
    }
    throw "$link already exists and is not a junction to $repoRoot. Remove it manually."
}

New-Item -ItemType Junction -Path $link -Target $repoRoot | Out-Null
Write-Host "linked $link -> $repoRoot"
Write-Host ''
Write-Host 'Next: enable "GlobalOverhaul" in the in-game mod list.'
Write-Host 'LuaCs (workshop id 2559634234) must be enabled as well - it compiles CSharp/Shared.'
