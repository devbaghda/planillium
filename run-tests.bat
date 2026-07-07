@echo off
"%LOCALAPPDATA%\Programs\Python\Python311\python.exe" -m pytest "%~dp0tests" -q
pause
