# PostToolUse(Write|Edit) hook: format the just-edited C# file with `dotnet format`.
# Reads the hook payload (JSON) from stdin, extracts the file path, and formats
# only that file if it's a .cs file. Always exits 0 so it never blocks edits.
$ErrorActionPreference = 'SilentlyContinue'

$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }

try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

$file = $payload.tool_input.file_path
if (-not $file) { $file = $payload.tool_response.filePath }
if (-not $file) { exit 0 }
if (-not $file.EndsWith('.cs')) { exit 0 }

# Project lives one repo-level up from .claude/hooks/ — derive it so this works
# on any teammate's machine without a hardcoded path.
$project = Join-Path $PSScriptRoot '..\..\SupportTicketSystem'

dotnet format $project --include $file --verbosity quiet | Out-Null
exit 0
