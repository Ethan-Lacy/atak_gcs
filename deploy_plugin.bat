@echo off
echo =============================================
echo Agent Manager Plugin Deployment Script
echo =============================================
echo.

set SOURCE_DIR=WinTAK Plugin (5.0)_AgentManager\bin\x64\Debug
set DEST_DIR=%appdata%\wintak\plugins\AgentManagerPlugin

echo Copying plugin files...
xcopy "%SOURCE_DIR%\AgentManagerPlugin.dll" "%DEST_DIR%\" /y
xcopy "%SOURCE_DIR%\AgentManagerPlugin.pdb" "%DEST_DIR%\" /y

echo.
echo Copying MAVLink dependency...
xcopy "%SOURCE_DIR%\MAVLink.dll" "%DEST_DIR%\" /y

echo.
echo =============================================
echo Deployment complete!
echo =============================================
echo.
echo Plugin location: %DEST_DIR%
echo.
echo Next steps:
echo 1. Close WinTAK (if running)
echo 2. Run this script again if copy failed
echo 3. Start WinTAK
echo 4. Look for "Agent Manager" button in toolbar
echo.
pause
