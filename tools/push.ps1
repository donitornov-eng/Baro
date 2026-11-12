# ---------------------------------------------------------------------------
#  Pushes the current branch to GitHub using the GITHUB_TOKEN stored as a
#  user-level environment variable.
#
#  Why this exists: the Windows Credential Manager has a saved login for a
#  different GitHub account, which makes a plain `git push` either fail with
#  403 or hang on a hidden credential prompt. This script bypasses the
#  credential helper for a single command and never writes the token to disk
#  or into .git/config.
#
#  Usage:
#      pwsh tools/push.ps1
#      pwsh tools/push.ps1 -Branch main -Remote origin
# ---------------------------------------------------------------------------
[CmdletBinding()]
param(
    [string]$Remote = 'origin',
    [string]$Branch = '',
    [string]$TokenVariable = 'GITHUB_TOKEN'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$token = [Environment]::GetEnvironmentVariable($TokenVariable, 'User')
if (-not $token) { $token = [Environment]::GetEnvironmentVariable($TokenVariable, 'Machine') }
if (-not $token) { throw "$TokenVariable is not set (user or machine scope)." }

if (-not $Branch) {
    $Branch = (& git -C $repoRoot rev-parse --abbrev-ref HEAD).Trim()
}

$basic = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("x-access-token:$token"))

$env:GIT_TERMINAL_PROMPT = '0'
& git -C $repoRoot -c credential.helper= -c "http.extraHeader=Authorization: Basic $basic" push $Remote $Branch
if ($LASTEXITCODE -ne 0) { throw "git push failed with exit code $LASTEXITCODE" }

Write-Host "pushed $Branch -> $Remote"
