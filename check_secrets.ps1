$input_str = "protocol=https`nhost=github.com`n`n"
$proc = Start-Process -FilePath "git" -ArgumentList "credential","fill" -RedirectStandardInput "$env:TEMP\gci.txt" -RedirectStandardOutput "$env:TEMP\gco.txt" -NoNewWindow -PassThru -Wait
$output = Get-Content "$env:TEMP\gco.txt" -Raw
$token = ($output -split "`n" | Where-Object { $_ -match "^password=" }) -replace "password=",""
if (-not $token) { Write-Host "no token found"; exit 1 }
$headers = @{ Authorization = "Bearer $($token.Trim())"; "X-GitHub-Api-Version" = "2022-11-28" }
try {
    $r = Invoke-RestMethod -Uri "https://api.github.com/repos/dvsmartinezgonzalez-dev/Claude-Code-Game-Studios/actions/secrets" -Headers $headers
    Write-Host "Total secrets: $($r.total_count)"
    $r.secrets | ForEach-Object { Write-Host "$($_.name) (updated $($_.updated_at))" }
} catch {
    Write-Host "API error: $($_.Exception.Message)"
}
