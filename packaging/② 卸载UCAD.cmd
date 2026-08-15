@echo off
chcp 65001 >nul
setlocal DisableDelayedExpansion
set "ROOT=%~dp0"
set "UNINSTALLER=%ROOT%payload\Uninstall.ps1"

if not exist "%UNINSTALLER%" (
  echo [UCAD] Uninstallation failed: payload\Uninstall.ps1 was not found.
  call :wait
  exit /b 2
)

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%UNINSTALLER%"
set "EXITCODE=%ERRORLEVEL%"

if not "%EXITCODE%"=="0" (
  echo.
  echo [UCAD] Uninstallation failed. Exit code: %EXITCODE%
  call :wait
  exit /b %EXITCODE%
)

echo.
echo [UCAD] Uninstallation completed.
call :wait
exit /b 0

:wait
if defined UCAD_NO_PAUSE exit /b 0
pause
exit /b 0
