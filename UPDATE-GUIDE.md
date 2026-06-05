## Jumong POS - Online Update Guide

### Architecture
- **Cloud**: `https://api-production-99fb.up.railway.app` (Railway, Dockerfile at root)
- **Version endpoint**: `GET /api/dashboard/version` returns `{version, buildDate, changes}`
- **Exe download**: Served as static file at `https://api-production-99fb.up.railway.app/updates/JumongPosV1.01.exe`
- **Client**: Desktop .NET 8 Windows app, published as single-file exe at `publish\JumongPosV1.01.exe`

### How the UPDATE button works
1. Client clicks UPDATE in Settings > CLOUD SYNC
2. Calls `/api/dashboard/version` — compares cloud version with local hardcoded version in `Services/UpdateService.cs`
3. If cloud version > local: shows changes, prompts download
4. Downloads `/updates/JumongPosV1.01.exe` → saves as `.new` → moves current to `.bak` → renames `.new` to exe → runs batch script to cleanup and restart

### Publishing an update (step by step)
1. **Bump version** in `JumongCloudAPI/Controllers/DashboardController.cs` (the `"version": "1.0.X"` line)
2. **Build desktop**: `dotnet publish -c Release --self-contained -r win-x64 -p:PublishSingleFile=true` from `C:\Users\ADMIN\Desktop\JumongPosV1.01`
3. **Copy exe to cloud static files**:
   ```
   New-Item -Force -ItemType Directory -Path "C:\Users\ADMIN\Desktop\JumongPosV1.01\JumongCloudAPI\wwwroot\updates"
   Copy-Item -Force "C:\Users\ADMIN\Desktop\JumongPosV1.01\publish\JumongPosV1.01.exe" "C:\Users\ADMIN\Desktop\JumongPosV1.01\JumongCloudAPI\wwwroot\updates\"
   ```
4. **Deploy to Railway**: `railway up` from `C:\Users\ADMIN\Desktop\JumongPosV1.01`
5. Clients click UPDATE → auto-download and restart

### Key files
| File | Purpose |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | Version endpoint (bump version here) |
| `Services/UpdateService.cs` | Client-side update logic (check, download, replace) |
| `Forms/SettingsForm.cs` | UPDATE button UI (CLOUD SYNC section) |
| `JumongCloudAPI/wwwroot/updates/JumongPosV1.01.exe` | Exe served to clients |
| `Dockerfile` (root) | Railway build config (builds JumongCloudAPI from subdirectory) |
| `.railwayignore` | Excludes desktop app source from Railway uploads |

### Notes
- Do NOT include `wwwroot/updates/` in `.railwayignore` — it must be deployed for download
- The 70MB exe makes Railway deploys slower — only upload when publishing an update
- After deploying an update, clean up `wwwroot/updates/` if not needed to speed up future API-only deploys
