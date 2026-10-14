# Проверки FitMind

Из корня рабочей области:

```powershell
dotnet build FitMind\FitMind.sln --no-restore --nologo
dotnet test FitMind\FitMind.Tests\FitMind.Tests.csproj --no-build --nologo
```

Тестовый проект создаёт отдельные временные базы SQL Server и проверяет аккаунты,
Google-вход, CSRF, права владельца и пользовательские сценарии. Рабочая база `FitMind`
тестами не очищается и не пересоздаётся. Учётные данные и внешние ключи задаются только
через environment variables или User Secrets.
