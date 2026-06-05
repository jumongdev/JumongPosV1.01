try {
$s = Invoke-RestMethod -Uri "https://api-production-99fb.up.railway.app/api/dashboard/recent-sales?limit=5" -TimeoutSec 10;
$s | ConvertTo-Json -Depth 1
} catch { Write-Host "Error: $_" }
