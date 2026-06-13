try {
$s = Invoke-RestMethod -Uri "https://jumong-pos-api-p285q.ondigitalocean.app/api/dashboard/recent-sales?limit=5" -TimeoutSec 10;
$s | ConvertTo-Json -Depth 1
} catch { Write-Host "Error: $_" }
