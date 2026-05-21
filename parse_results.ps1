[xml]$xml = Get-Content 'C:\Users\dvsma\Desktop\boltsort\test-results.xml'
$failed = $xml.SelectNodes('//test-case[@result="Failed"]')
foreach ($t in $failed) {
    Write-Host "FAIL: $($t.fullname)"
    $msg = $t.SelectSingleNode('failure/message')
    $stk = $t.SelectSingleNode('failure/stack-trace')
    if ($msg) { Write-Host "  Msg: $($msg.InnerText.Trim())" }
    if ($stk)  { Write-Host "  At:  $($stk.InnerText.Trim().Split("`n")[0])" }
}
