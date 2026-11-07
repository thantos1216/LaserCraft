param([Parameter(ValueFromRemainingArguments=$true)][string[]]$ReaArguments)
$ErrorActionPreference = 'Stop'
$reaRoot = 'D:\Development\REA\rea'
$nodePath = 'C:\Users\thant\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
if (!(Test-Path -LiteralPath "$reaRoot\dist")) { throw 'REA is not built. See docs/research/rea-findings.md.' }
& $nodePath "$reaRoot\scripts\rea.mjs" @ReaArguments
exit $LASTEXITCODE
