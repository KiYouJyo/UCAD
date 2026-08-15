@echo off
chcp 65001 >nul
setlocal
set "ROOT=%~dp0"
set "SCRIPT=%ROOT%payload\Install.ps1"
if not exist "%SCRIPT%" (
  echo Installation failed: payload\Install.ps1 was not found.
  pause
  exit /b 2
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" (
  echo UCAD installation failed. Exit code: %EXITCODE%
  pause
  exit /b %EXITCODE%
)
echo UCAD installation completed.
pause
exit /b 0
