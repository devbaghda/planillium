@echo off
echo Stopping running instance...
taskkill /IM MentorOverseer.exe /F 2>nul
timeout /t 1 /nobreak >nul

echo Building...
"C:\Users\devba\AppData\Local\Programs\Python\Python311\Scripts\pyinstaller.exe" ^
  --onefile --noconsole --icon icon.ico --distpath . --name MentorOverseer main.py

echo Done.
pause
