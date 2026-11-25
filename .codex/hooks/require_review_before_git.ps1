$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$inputJson = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($inputJson))
{
    exit 0
}

try
{
    $payload = $inputJson | ConvertFrom-Json
}
catch
{
    exit 0
}

$command = [string]$payload.tool_input.command
if ([string]::IsNullOrWhiteSpace($command))
{
    exit 0
}

$isCommitOrPush = $command -match '(?i)(^|[;&|]\s*)(?:[^\s;&|]*git(?:\.exe)?|git)\s+(?:-[^\s]+\s+)*(commit|push)\b'
if (-not $isCommitOrPush)
{
    exit 0
}

try
{
    $repoRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
}
catch
{
    $repoRoot = $payload.cwd
}

if ([string]::IsNullOrWhiteSpace($repoRoot))
{
    $repoRoot = Get-Location
}

$approvalPath = Join-Path $repoRoot ".codex/review-approved.flag"
if (Test-Path -LiteralPath $approvalPath)
{
    Remove-Item -LiteralPath $approvalPath -Force
    exit 0
}

$reason = "커밋 또는 푸쉬 전에 코드 리뷰 에이전트를 생성해 CONVENTIONS.md 기준의 버그 체크와 코드 컨벤션 검사를 실행해야 합니다. 리뷰가 끝난 뒤 .codex/review-approved.flag 파일을 생성하고 같은 git 명령을 다시 실행하세요."
$output = @{
    hookSpecificOutput = @{
        hookEventName = "PreToolUse"
        permissionDecision = "deny"
        permissionDecisionReason = $reason
    }
}

$output | ConvertTo-Json -Depth 5 -Compress
exit 0
