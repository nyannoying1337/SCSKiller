@echo off
rem Double-click: runs compact-test.ps1 next to this file (see there).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0compact-test.ps1" %*
pause
