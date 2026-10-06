@echo off
rem Double-click: runs driver-strings.ps1 next to this file (see there).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0driver-strings.ps1" %*
pause
