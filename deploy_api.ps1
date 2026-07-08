$src = "C:\Users\ADMIN\Desktop\JumongPosV1.01\JumongCloudAPI\bin\Release\net8.0\win-x64\publish"
$dst = "C:\JumongAPI"
net stop JumongCloudAPI
Start-Sleep 3
Copy-Item -Recurse -Force "$src\*" "$dst"
net start JumongCloudAPI
Write-Host "Deploy done"
