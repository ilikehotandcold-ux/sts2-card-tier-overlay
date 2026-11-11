@echo off
title Uninstall Card Tier Overlay
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -Uninstall
echo.
pause
