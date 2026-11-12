<#
.SYNOPSIS
    Regenerates the decompiled reference tree in _ref/ and the type index.

.DESCRIPTION
    Barotrauma and LuaCs both update often, and every signature in the C# mod has
    to match the installed build. This script decompiles the exact assemblies the
    game is running (ilspycmd) and rebuilds _ref/index.txt.

    Requires: dotnet + ilspycmd (dotnet tool install -g ilspycmd).

.PARAMETER Force
    Re-decompile even if the output folders already exist.

.EXAMPLE
    pwsh tools/refresh-ref.ps1 -Force
#>
[CmdletBinding()]
param(
    [string]$GameDir = 'D:\Steam\steamapps\common\Barotrauma',
    [string]$LuaCsDir = 'D:\Steam\steamapps\workshop\content\602960\2559634234',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$refRoot = Join-Path $repoRoot '_ref'

function Assert-Path([string]$Path, [string]$What) {
    if (-not (Test-Path $Path)) { throw "$What not found: $Path" }
}

function Get-GameVersion([string]$AssemblyPath) {
    return (Get-Item $AssemblyPath).VersionInfo.FileVersion
}

function Invoke-Decompile([string]$AssemblyPath, [string]$OutDir) {
    if ((Test-Path $OutDir) -and -not $Force) {
        Write-Host "skip  $OutDir (already exists, use -Force to rebuild)"
        return
    }
    if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
    Write-Host "decompile $AssemblyPath -> $OutDir"
    & ilspycmd -p -o $OutDir $AssemblyPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "ilspycmd failed for $AssemblyPath" }
}

function Build-TypeIndex([string]$Root, [string]$IndexPath) {
    $decl = [regex]'^\s*(?:public|internal|protected|private)?\s*(?:static\s+|abstract\s+|sealed\s+|partial\s+|readonly\s+|unsafe\s+|ref\s+)*(class|struct|interface|enum|record)\s+([A-Za-z_][A-Za-z0-9_]*)'
    $lines = [System.Collections.Generic.List[string]]::new()
    Get-ChildItem $Root -Recurse -Filter *.cs | ForEach-Object {
        $relative = [System.IO.Path]::GetRelativePath($Root, $_.FullName)
        foreach ($line in [System.IO.File]::ReadLines($_.FullName)) {
            $m = $decl.Match($line)
            if ($m.Success) { $lines.Add("$($m.Groups[2].Value)`t$relative") }
        }
    }
    $lines | Sort-Object -Unique | Set-Content $IndexPath -Encoding utf8
    Write-Host "index   $IndexPath ($($lines.Count) entries)"
}

Assert-Path $GameDir 'Game directory'
Assert-Path $LuaCsDir 'LuaCsForBarotrauma mod directory'
Assert-Path (Join-Path $GameDir 'Barotrauma.dll') 'Barotrauma.dll'

if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) {
    throw 'ilspycmd is not installed. Run: dotnet tool install -g ilspycmd'
}

$luacsAssembly = Join-Path $LuaCsDir 'Binary/Barotrauma.dll'
Write-Host "game version:  $(Get-GameVersion (Join-Path $GameDir 'Barotrauma.dll'))"
Write-Host "luacs version: $(Get-GameVersion $luacsAssembly)"

Invoke-Decompile (Join-Path $GameDir 'Barotrauma.dll') (Join-Path $refRoot 'project')
Invoke-Decompile (Join-Path $GameDir 'BarotraumaCore.dll') (Join-Path $refRoot 'project-core')
Invoke-Decompile (Join-Path $LuaCsDir 'Binary/Barotrauma.dll') (Join-Path $refRoot 'luacs')

Build-TypeIndex (Join-Path $refRoot 'project') (Join-Path $refRoot 'index.txt')

Write-Host 'done. Update the version table in _ref/README.md if the game version changed.'
