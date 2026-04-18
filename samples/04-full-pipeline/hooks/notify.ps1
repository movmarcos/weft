# Windows counterpart
$json = [Console]::In.ReadToEnd()
$phase = $env:WEFT_HOOK_PHASE
$profile = $env:WEFT_HOOK_PROFILE
$db = $env:WEFT_HOOK_DATABASE

Write-Host "[${phase}] profile=${profile} db=${db}"
Write-Host "  payload: $($json.Substring(0, [Math]::Min(200, $json.Length)))..."
