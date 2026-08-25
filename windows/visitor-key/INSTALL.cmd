@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Visitor-Key.ps1" -PublishedPath "%~dp0publish"
if errorlevel 1 (
  echo.
  echo Installation did not complete. Leave this window open and report the error above.
  pause
  exit /b 1
)
echo.
echo LOKWOD Visitor Key v1.0.2 is installed and running.
timeout /t 4 >nul
