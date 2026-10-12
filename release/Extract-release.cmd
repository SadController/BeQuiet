@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0extract.ps1"
if errorlevel 1 (
  echo Release extraction failed.
  pause
  exit /b 1
)
echo Release ready. Open BeQuiet-1.2.0-win-x64 and run BeQuiet.exe.
pause
