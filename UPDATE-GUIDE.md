## Jumong POS - Update Guide

### Architecture
- **Cloud**: `https://jumong-pos-api-p285q.ondigitalocean.app` (DigitalOcean App Platform)
- **Exe download**: GitHub Releases at `https://github.com/jumongdev/JumongPosV1.01/releases`
- **Client**: Desktop .NET 8 Windows app, published as single-file exe

### How the UPDATE button works
1. Client clicks UPDATE in Settings > CLOUD SYNC
2. Calls `/api/dashboard/version` — compares cloud version with local hardcoded version in `Services/UpdateService.cs`
3. If cloud version > local: shows changes, prompts download
4. Downloads from GitHub Releases → saves as `.new` → moves current to `.bak` → renames `.new` to exe

### Publishing an update (step by step)
1. **Bump version** in `Services/AppVersion.cs`
2. **Update cloud version** in `JumongCloudAPI/Controllers/DashboardController.cs` (version endpoint)
3. **Build desktop**: `dotnet publish -c Release -r win-x64 --self-contained true`
4. **Create GitHub release**: `gh release create v1.0.XX "publish\v1.0.XX\JumongPosV1.01.exe" --title "v1.0.XX" --notes "Changes" --repo jumongdev/JumongPosV1.01`
5. **Deploy cloud API**: `git push origin master` (auto-deploys to DigitalOcean)
6. Clients click UPDATE → auto-download and restart

### Key files
| File | Purpose |
|---|---|
| `JumongCloudAPI/Controllers/DashboardController.cs` | Version endpoint (cloud-exposed version string) |
| `Services/UpdateService.cs` | Client-side update logic (check, download, replace) |
| `Services/AppVersion.cs` | Local hardcoded version string |
| `Forms/SettingsForm.cs` | UPDATE button UI (CLOUD SYNC section) |
