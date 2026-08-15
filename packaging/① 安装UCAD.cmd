@echo off
chcp 65001 >nul
setlocal DisableDelayedExpansion
set "ROOT=%~dp0"
set "INSTALLER=%ROOT%payload\Install.ps1"

if not exist "%INSTALLER%" (
  echo [UCAD] Installation failed: payload\Install.ps1 was not found.
  call :wait
  exit /b 2
)

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%INSTALLER%"
set "EXITCODE=%ERRORLEVEL%"

if not "%EXITCODE%"=="0" (
  echo.
  echo [UCAD] Installation failed. Exit code: %EXITCODE%
  call :wait
  exit /b %EXITCODE%
)

echo.
echo [UCAD] Installation completed.
call :wait
exit /b 0

:wait
if defined UCAD_NO_PAUSE exit /b 0
pause
exit /b 0
