# JumongPOS - Build & Deploy Guide

## Version
**Current:** 1.0.19

## Client App (WinForms)

### Build
```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```
Output: `bin\Release\net8.0-windows\win-x64\publish\JumongPosV1.01.exe`

### Publish new release
```powershell
# Build
dotnet publish -c Release -r win-x64 --self-contained true -o publish\v1.0.XX

# Create GitHub release
gh release create v1.0.XX "publish\v1.0.XX\JumongPosV1.01.exe" `
  --title "v1.0.XX" `
  --notes "Description of changes" `
  --repo jumongdev/JumongPosV1.01
```

### Update via in-app button
Clients open **Settings > UPDATE** — it checks GitHub Releases, downloads, and auto-installs.

## Cloud API (ASP.NET Core)

### Deploy to DigitalOcean
```powershell
# Push code to GitHub — auto-build if connected, or:
git push origin master

# Or trigger manual deploy via API:
# GET https://api.digitalocean.com/v2/apps/{app_id}/deployments
# POST with {"force_build": true}
```

## Database

### Local SQLite
- Auto-created at `JumongPos.db` in app directory
- Migrations run automatically on startup (`DatabaseHelper.Initialize()`)

### Cloud PostgreSQL
- Hosted on DigitalOcean (`db-s-1vcpu-1gb`)
- Connection via DATABASE_URL env var
- **Firewall:** Only allows App Platform. Add IP via DO API if needed:
```powershell
# PUT https://api.digitalocean.com/v2/databases/{db_id}/firewall
# Body: {"rules": [{"type": "app", "value": "{app_id}"}, {"type": "ip_addr", "value": "{your_ip}"}]}
```

## What Was Fixed (Profit/Margin)

| File | Change |
|---|---|
| `Forms/SalesForm.cs:456` | Sets `UnitCost` when adding item to cart |
| `JumongCloudAPI/DashboardController.cs` | 3 queries now fallback to `p.cost` when `unit_cost = 0` |
| PostgreSQL | Backfilled 36,002 historic sale_items with product costs |

## Common Tasks

### Run SQL query on cloud DB
Use a temp .NET project with Npgsql, adding your IP to firewall first.

### Check deployed API version
```powershell
Invoke-RestMethod https://jumong-pos-api-p285q.ondigitalocean.app/api/dashboard/version
```
