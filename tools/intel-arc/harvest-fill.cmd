@echo off
rem Right-click, Run as administrator: harvest-test.ps1 with -Fill (long: fills under harvest mode to see whether 512 MB applies).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0harvest-test.ps1" -Fill %*
pause
