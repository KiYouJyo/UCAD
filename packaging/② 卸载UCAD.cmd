@echo off
chcp 65001 >nul
setlocal
set "ROOT=%~dp0"
set "SCRIPT=%ROOT%payload\Uninstall.ps1"
if not exist "%SCRIPT%" (
  echo Uninstallation failed: payload\Uninstall.ps1 was not found.
  pause
  exit /b 2
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" (
  echo UCAD uninstallation failed. Exit code: %EXITCODE%
  pause
  exit /b %EXITCODE%
)
echo UCAD uninstallation completed.
pause
exit /b 0
