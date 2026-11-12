# ---------------------------------------------------------------------------
#  Launcher for the local GitHub MCP server (stdio).
#
#  OpenCode starts this script as an MCP server. The GitHub token is never
#  written to a config file: it is resolved at launch time, in this order:
#
#    1. GITHUB_PERSONAL_ACCESS_TOKEN already in the environment
#    2. GITHUB_TOKEN in the environment of the OpenCode process
#    3. GITHUB_TOKEN stored as a user-level variable (survives service starts)
#
#  Prerequisites (once):
#      npm install -g @modelcontextprotocol/server-github
#      [Environment]::SetEnvironmentVariable('GITHUB_TOKEN','ghp_...','User')
# ---------------------------------------------------------------------------
$ErrorActionPreference = 'Stop'

$token = $env:GITHUB_PERSONAL_ACCESS_TOKEN
if (-not $token) { $token = $env:GITHUB_TOKEN }
if (-not $token) { $token = [Environment]::GetEnvironmentVariable('GITHUB_TOKEN', 'User') }

if (-not $token) {
    [Console]::Error.WriteLine('github-mcp: GITHUB_TOKEN is not set - GitHub MCP server cannot authenticate.')
    exit 1
}

$env:GITHUB_PERSONAL_ACCESS_TOKEN = $token

$exe = Join-Path $env:APPDATA 'npm\mcp-server-github.cmd'
if (-not (Test-Path $exe)) {
    [Console]::Error.WriteLine("github-mcp: $exe not found. Run: npm install -g @modelcontextprotocol/server-github")
    exit 1
}

& $exe
exit $LASTEXITCODE
