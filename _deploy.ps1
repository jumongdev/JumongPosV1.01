Stop-Service JumongCloudAPI -ErrorAction Stop
Start-Sleep 3
Copy-Item -Recurse "C:\Users\ADMIN\Desktop\JumongPosV1.01\JumongCloudAPI\bin\Release\net8.0\win-x64\publish\*" "C:\JumongAPI\" -Force
Start-Sleep 1
Start-Service JumongCloudAPI
Write-Host "DONE"
