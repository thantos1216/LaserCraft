param([Parameter(ValueFromRemainingArguments=$true)][string[]]$ReaArguments)
$ErrorActionPreference = 'Stop'
# Paths passed to REA are container paths. See docs/research/rea-findings.md.
$container = 'rea-rea-1'
& docker exec -i $container node /workspace/scripts/rea.mjs @ReaArguments
exit $LASTEXITCODE
