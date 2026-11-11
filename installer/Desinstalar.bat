@echo off
title Desinstalar Card Tier Overlay
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0instalar.ps1" -Uninstall
echo.
pause
