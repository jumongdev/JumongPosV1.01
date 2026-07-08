@echo off
net stop JumongCloudAPI
timeout /t 3 /nobreak >nul
copy /y "C:\Users\ADMIN\Desktop\JumongPosV1.01\JumongCloudAPI\bin\Release\net8.0\win-x64\publish\*.exe" "C:\JumongAPI\"
copy /y "C:\Users\ADMIN\Desktop\JumongPosV1.01\JumongCloudAPI\bin\Release\net8.0\win-x64\publish\*.dll" "C:\JumongAPI\"
copy /y "C:\Users\ADMIN\Desktop\JumongPosV1.01\JumongCloudAPI\bin\Release\net8.0\win-x64\publish\*.json" "C:\JumongAPI\"
xcopy /e /y "C:\Users\ADMIN\Desktop\JumongPosV1.01\JumongCloudAPI\bin\Release\net8.0\win-x64\publish\wwwroot" "C:\JumongAPI\wwwroot\"
net start JumongCloudAPI
echo API DEPLOYED SUCCESSFULLY!
pause
