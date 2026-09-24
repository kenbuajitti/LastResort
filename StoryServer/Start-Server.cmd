@echo off
cd /d "%~dp0"
where node >nul 2>nul
if errorlevel 1 (
  echo Install Node.js 22 or newer, then run this file again.
  pause
  exit /b 1
)
if not exist .env (
  copy .env.example .env >nul
  echo Enter your OpenAI API key in the .env file, save it, and close Notepad.
  start /wait notepad.exe .env
)
node --env-file=.env server.mjs
pause
