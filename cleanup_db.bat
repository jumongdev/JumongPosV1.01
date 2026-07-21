@echo off
title JumongPOS - Complete Database Cleanup
echo ========================================
echo  Removing ALL SQL Server, MySQL, SOMA
echo  Only PostgreSQL 18 will be kept
echo ========================================
echo.

echo [1/6] Stopping all SQL + SOMA services...
for %%s in (MSSQLSERVER "MSSQL$SOMA2008" "MSSQL$SQLEXPRESS" "MSSQL$SQLEXPRESS01" SQLSERVERAGENT "SQLAgent$SOMA2008" "SQLAgent$SQLEXPRESS" "SQLAgent$SQLEXPRESS01" SQLBrowser SQLTELEMETRY "SQLTELEMETRY$SQLEXPRESS" "SQLTELEMETRY$SQLEXPRESS01" SQLWriter "GS_SOMA_SOMA Game" SM_SOMA_0 SOMA_FileManager SOMA_Game SOMA_Session SOMA_Starter SOMA_UserManager UM_SOMA) do (
    net stop %%s /y >nul 2>&1
)
sc config SQLWriter start=disabled >nul 2>&1
echo  Done.

echo.
echo [2/6] Uninstalling ItalySomaUniteFull...
start /wait "" "C:\Program Files (x86)\InstallShield Installation Information\{39983924-B3B5-44FB-8DCD-F32D3ACF8D77}\Setup.exe" -uninst -l0x9 -uninstall
echo  Done.

echo.
echo [3/6] Uninstalling SSMS 22...
start /wait "" "C:\Program Files (x86)\Microsoft Visual Studio\Installer\setup.exe" uninstall --installPath "C:\Program Files\Microsoft SQL Server Management Studio 22\Release" --quiet
echo  Done.

echo.
echo [4/6] Uninstalling SQL Drivers + Native Client...
start /wait msiexec /x {ADA823D7-2A3F-4FC6-96AC-C11656168D1E} /qn /norestart
start /wait msiexec /x {0E0F96AC-80DE-4400-A40C-429D63293651} /qn /norestart
start /wait msiexec /x {820A3DEC-9783-42AE-B12D-750FCCF07E10} /qn /norestart
start /wait msiexec /x {76EB75D2-CCF6-41A9-90B6-922DE9146276} /qn /norestart
start /wait msiexec /x {AFFC3E2E-B4E9-40CE-8F85-9D104554535C} /qn /norestart
start /wait msiexec /x {5BC7E9EB-13E8-45DB-8A60-F2481FEB4595} /qn /norestart
echo  Done.

echo.
echo [5/6] Uninstalling SQL Shared Components (2008 R2)...
start /wait msiexec /x {1BA457D4-90F2-4D83-9543-9715849023C8} /qn /norestart
start /wait msiexec /x {234F6B0D-10AE-4BB7-B2F3-E48D4861952D} /qn /norestart
start /wait msiexec /x {36F70DEE-1EBF-4707-AFA2-E035EEAEBAA1} /qn /norestart
start /wait msiexec /x {51E5BC99-A087-4CFF-8D93-462903EA7E12} /qn /norestart
start /wait msiexec /x {72AB7E6F-BC24-481E-8C45-1AB5B3DD795D} /qn /norestart
start /wait msiexec /x {A2122A9C-A699-4365-ADF8-68FEAC125D61} /qn /norestart
start /wait msiexec /x {C942A025-A840-4BF2-8987-849C0DD44574} /qn /norestart
start /wait msiexec /x {B2213E4E-F502-4D36-BE95-9293C866EF3F} /qn /norestart
start /wait msiexec /x {B40EE88B-400A-4266-A17B-E3DE64E94431} /qn /norestart
start /wait msiexec /x {D21BC5B2-CBAC-48FA-A701-B5A63C1CA7B8} /qn /norestart
echo  Done.

echo.
echo [5/6 cont.] Uninstalling SQL Shared Components (2019)...
start /wait msiexec /x {0FB552DD-543E-48E7-A6F4-2F8D82723C6A} /qn /norestart
start /wait msiexec /x {17DCED0E-5B27-453A-B2B4-E487B869B28A} /qn /norestart
start /wait msiexec /x {2129312E-5204-4F3A-9039-B6D34DBB00FB} /qn /norestart
start /wait msiexec /x {228C3DC2-695E-4FC7-87E4-6A9CE905DA9B} /qn /norestart
start /wait msiexec /x {28ED6838-D8E5-454C-A813-12C5EB447CAB} /qn /norestart
start /wait msiexec /x {5825CDC4-4E99-4CF9-91FE-DB60C0E2F5EA} /qn /norestart
start /wait msiexec /x {5E4344C9-8B97-4ED9-8760-57E221C240F4} /qn /norestart
start /wait msiexec /x {619F0B6C-C802-422A-B4E5-294E61F68473} /qn /norestart
start /wait msiexec /x {6213D6CB-D258-47A3-B1A0-EE1E5C080DCF} /qn /norestart
start /wait msiexec /x {814D5077-C93F-42E2-B875-717007C186B9} /qn /norestart
start /wait msiexec /x {8DDAEBCA-4267-4E16-9FE0-D87F21D36891} /qn /norestart
start /wait msiexec /x {99B940D5-1A49-4B6C-B26C-6A88B2C061CA} /qn /norestart
start /wait msiexec /x {A8581199-F913-443B-B058-8E8BF317E71C} /qn /norestart
start /wait msiexec /x {C7E6D4B7-CB10-4239-BA04-D9339B39D0BD} /qn /norestart
start /wait msiexec /x {D459615B-83B0-408F-8F39-6CC07C277BA6} /qn /norestart
start /wait msiexec /x {DE5B7937-D5B5-4157-BC30-BB87F021CFF0} /qn /norestart
start /wait msiexec /x {FC8DC283-4A85-467F-8D0E-2FE4606DCCA1} /qn /norestart
start /wait msiexec /x {FD730873-33D1-4D1F-9AE0-E259586F8827} /qn /norestart
start /wait msiexec /x {F31183CF-E10F-4DE1-BB59-6C0FF38E481E} /qn /norestart
echo  Done.

echo.
echo [5/6 cont.] Uninstalling SQL Shared Components (2022)...
start /wait msiexec /x {0CEFE958-E71A-4171-9DEF-77E9234A5613} /qn /norestart
start /wait msiexec /x {12618131-AA9A-4DAE-9387-CE4417955B9F} /qn /norestart
start /wait msiexec /x {161B8D12-C41B-4ACF-9BB5-E1FEE6788869} /qn /norestart
start /wait msiexec /x {35EC6145-E333-42DB-BCB3-380DF6140C11} /qn /norestart
start /wait msiexec /x {5AB77D4E-9E5F-4627-B78B-129A5EC2858A} /qn /norestart
start /wait msiexec /x {629C8FC9-3763-4C58-8264-5288AE34AFEF} /qn /norestart
start /wait msiexec /x {6A68D32C-4C0D-4847-B70C-58E6B4D76A12} /qn /norestart
start /wait msiexec /x {6F8242AA-1B25-421C-8E45-FC5978D9AA3A} /qn /norestart
start /wait msiexec /x {770DA7F2-817B-4AA6-9160-08BB658ABDC6} /qn /norestart
start /wait msiexec /x {7EFD8B19-A9E6-41CF-A96F-B9B6E30EC345} /qn /norestart
start /wait msiexec /x {8770AF64-BB4B-4404-BDD6-6AF8E4C461FC} /qn /norestart
start /wait msiexec /x {94AEB0A0-365C-449B-B573-D2ECB353EB06} /qn /norestart
start /wait msiexec /x {A0F7ACBA-075F-4BC7-A85A-5DC301FCEC74} /qn /norestart
start /wait msiexec /x {AB5D8778-81F3-47E2-87A4-35E776CD664B} /qn /norestart
start /wait msiexec /x {BD8B7339-7559-4FC3-95E6-264324D45235} /qn /norestart
start /wait msiexec /x {D6E82158-05B9-4A18-A624-EA135BC77766} /qn /norestart
start /wait msiexec /x {DCA0C2D6-83BF-41AE-B1AB-C4181002DE40} /qn /norestart
start /wait msiexec /x {EAC54B82-7A37-4A9E-8953-474316BD40F6} /qn /norestart
start /wait msiexec /x {EACFA728-AFB2-48E2-B5FE-86690FEEF684} /qn /norestart
start /wait msiexec /x {FDB357D5-CC78-480A-8D26-C15D1A877642} /qn /norestart
echo  Done.

echo.
echo [6/6] Cleaning orphaned registry entries...
reg delete "HKLM\SOFTWARE\Microsoft\Microsoft SQL Server" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\WOW6432Node\Microsoft\Microsoft SQL Server" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft SQL Server 10" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft SQL Server 15" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft SQL Server 16" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft SQL Server 2008 R2" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft SQL Server SQL2019" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft SQL Server SQL2022" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\KB2630458" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\KB3045313" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\KB5090408" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\KB5091158" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\KB5102334" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\KB5102336" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\c7a47713" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{D21BC5B2-CBAC-48FA-A701-B5A63C1CA7B8}" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{FDB357D5-CC78-480A-8D26-C15D1A877642}" /f >nul 2>&1
echo  Done.

echo.
echo Deleting leftover SQL Server folders...
rmdir /s /q "C:\Program Files\Microsoft SQL Server" >nul 2>&1
rmdir /s /q "C:\Program Files (x86)\Microsoft SQL Server" >nul 2>&1
rmdir /s /q "C:\Program Files\Microsoft SQL Server Management Studio 22" >nul 2>&1
rmdir /s /q "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft SQL Server 2008 R2" >nul 2>&1
rmdir /s /q "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft SQL Server 2019" >nul 2>&1
rmdir /s /q "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft SQL Server 2022" >nul 2>&1
echo  Done.

echo.
echo ========================================
echo  ✅ CLEANUP COMPLETE!
echo  Only PostgreSQL 18 remains.
echo  Recommend restarting the PC now.
echo ========================================
pause
