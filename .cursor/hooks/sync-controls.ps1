# Reminds the agent to keep Help / README / AGENTS in sync when the control catalog changes.
$ErrorActionPreference = "Stop"

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) {
    Write-Output "{}"
    exit 0
}

try {
    $payload = $raw | ConvertFrom-Json
}
catch {
    Write-Output "{}"
    exit 0
}

$path = [string]$payload.file_path
if ([string]::IsNullOrWhiteSpace($path)) {
    Write-Output "{}"
    exit 0
}

$normalized = $path.Replace("\", "/")
$watched =
    $normalized -match "Bridge/ControlCatalog\.cs$" -or
    $normalized -match "UI/TimeControls\.cs$"

if (-not $watched) {
    Write-Output "{}"
    exit 0
}

$context = @"
ControlCatalog / atalhos foram editados. Mantenha sincronizados:
1. Bridge/ControlCatalog.cs (fonte: HelpLines, ReadmeSummary, AgentsMustMention)
2. README.md secao Controles (deve conter ControlCatalog.ReadmeSummary exatamente)
3. AGENTS.md paragrafo de controles (tokens em AgentsMustMention)
4. Rode: dotnet test --filter ControlCatalog
"@

$result = @{ additional_context = $context.Trim() } | ConvertTo-Json -Compress
Write-Output $result
exit 0
