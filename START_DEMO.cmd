@echo off
setlocal
cd /d "%~dp0FitMind"
set ASPNETCORE_ENVIRONMENT=Development
if not defined DemoAccount__Email (
  echo Set DemoAccount__Email in the current terminal before starting the demo.
  exit /b 1
)
if not defined DemoAccount__Password (
  echo Set DemoAccount__Password in the current terminal before starting the demo.
  exit /b 1
)
set DemoAccount__Enabled=true
echo FitMind uses SQL Server from ConnectionStrings:DefaultConnection in appsettings.json.
echo Demo credentials are read from environment variables and are not printed.
dotnet run --launch-profile http
pause
