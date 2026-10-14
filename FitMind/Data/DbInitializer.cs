using FitMind.Models;
using FitMind.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Data;

public static class DbInitializer
{
    private const string CurrentSeedVersion = "3";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        await DatabaseSchemaLifecycle.EnsureReadyAsync(db);
        var isSeeded = await db.SystemSettings.AnyAsync(x =>
            x.Key == "SeedVersion" && x.Value == CurrentSeedVersion);

        if (!isSeeded)
        {
            // Не добавляем стартовые строки поверх старой/частично заполненной базы.
            // Так приложение не создаст дубликаты и не повредит пользовательские данные.
            var hasExistingContent = await db.Exercises.AnyAsync()
                || await db.FoodProducts.AnyAsync()
                || await db.Programs.AnyAsync(x => x.OwnerId == null);
            if (hasExistingContent)
            {
                throw new InvalidOperationException(
                    "В локальной базе уже есть данные, но нет актуальной отметки заполнения. " +
                    "Сохраните резервную копию и укажите пустую базу в ConnectionStrings:DefaultConnection, " +
                    "чтобы приложение заполнило её из JSONData/database-seed.json.");
            }

            await JsonDatabaseSeeder.ImportAsync(db, environment.WebRootPath);
        }

        await EnsureNutritionProgramsAsync(db);
        await EnsureSleepProgramsAsync(db);
        await EnsureBaseRecipesAsync(db);
        await EnsureMainMeasurementTypesAsync(db);

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in new[] { "Пользователь", "Администратор" })
        {
            if (await roles.FindByNameAsync(roleName) is not null) continue;
            var result = await roles.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (config.GetValue<bool>("DemoAccount:Enabled"))
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var email = config["DemoAccount:Email"];
            var password = config["DemoAccount:Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException(
                    "Для DemoAccount укажите DemoAccount__Email и DemoAccount__Password через user-secrets или переменные окружения. Пароль в appsettings.json не хранится.");
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };
                var result = await users.CreateAsync(user, password);
                if (!result.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
            }

            if (!await users.IsInRoleAsync(user, "Пользователь"))
            {
                var roleResult = await users.AddToRoleAsync(user, "Пользователь");
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(x => x.Description)));
            }

            // Назначение первого администратора — только явной конфигурацией
            // окружения. Пароль и адрес не попадают в репозиторий или журнал.
            var bootstrapAdminEmail = config["AdminBootstrap:Email"];
            if (!string.IsNullOrWhiteSpace(bootstrapAdminEmail)
                && string.Equals(email, bootstrapAdminEmail, StringComparison.OrdinalIgnoreCase)
                && !await users.IsInRoleAsync(user, "Администратор"))
            {
                var adminResult = await users.AddToRoleAsync(user, "Администратор");
                if (!adminResult.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", adminResult.Errors.Select(x => x.Description)));
            }

            if (!await db.UserProfiles.AnyAsync(x => x.UserId == user.Id))
            {
                db.UserProfiles.Add(new UserProfile
                {
                    UserId = user.Id,
                    Name = "Пользователь FitMind",
                    Gender = "Не указан",
                    CreatedAt = DateTime.Now,
                    OnboardingStep = 3,
                    OnboardingCompleted = true
                });
                await db.SaveChangesAsync();
            }
        }
    }

    private static async Task EnsureNutritionProgramsAsync(ApplicationDbContext db)
    {
        if (await db.Programs.AnyAsync(x => x.OwnerId == null && x.Kind == ProgramKind.Nutrition)) return;
        var definitions = new[]
        {
            ("Снижение веса — 1 600 ккал", "Снижение веса", 1600, 4),
            ("Поддержание формы — 2 200 ккал", "Поддержание формы", 2200, 4),
            ("Набор массы — 2 800 ккал", "Набор мышечной массы", 2800, 5),
            ("Вегетарианская — 2 000 ккал", "Поддержание формы", 2000, 4),
            ("Четыре приёма пищи", "Сбалансированное питание", 2100, 4),
            ("Пять приёмов пищи", "Дробное питание", 2300, 5)
        };
        foreach (var (name, goal, calories, mealCount) in definitions)
        {
            var program = new FitnessProgram
            {
                Kind = ProgramKind.Nutrition, Name = name, GoalText = goal, CaloriesPerDay = calories,
                WaterMl = 2000, Description = "Вода и напитки без добавленного сахара.", Weeks = 4,
                Image = "/images/covers/unavailable.svg", Active = true, CreatedAt = DateTime.UtcNow
            };
            var names = new[] { "Завтрак", "Второй завтрак", "Обед", "Перекус", "Ужин" };
            var times = new[] { new TimeSpan(8, 0, 0), new TimeSpan(11, 0, 0), new TimeSpan(14, 0, 0), new TimeSpan(17, 0, 0), new TimeSpan(20, 0, 0) };
            for (var i = 0; i < mealCount; i++) program.Items.Add(new ProgramItem { Type = ProgramItemType.Meal, DayNumber = 1, Title = names[i], Time = times[i], Calories = calories / mealCount, Order = (short)(i + 1) });
            program.Items.Add(new ProgramItem { Type = ProgramItemType.Water, DayNumber = 1, Title = "Вода", Time = new TimeSpan(9, 0, 0), WaterMl = 2000, Order = (short)(mealCount + 1) });
            db.Programs.Add(program);
        }
        await db.SaveChangesAsync();
    }

    private static async Task EnsureSleepProgramsAsync(ApplicationDbContext db)
    {
        if (await db.Programs.AnyAsync(x => x.OwnerId == null && x.Kind == ProgramKind.Sleep)) return;
        var definitions = new[]
        {
            ("Сон 8 часов", "Стабильный восьмичасовой сон.", new TimeSpan(23, 0, 0), new TimeSpan(7, 0, 0), (string?)null, (TimeSpan?)null, (short?)null),
            ("Ранний подъём", "Постепенный переход к раннему началу дня.", new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), (string?)null, (TimeSpan?)null, (short?)null),
            ("Восстановление после силовых", "Сон и вечерняя процедура после нагрузки.", new TimeSpan(23, 0, 0), new TimeSpan(7, 0, 0), "Растяжка", (TimeSpan?)new TimeSpan(20, 30, 0), (short?)15),
            ("Неделя разгрузки", "Режим сна и мягкого восстановления на семь дней.", new TimeSpan(22, 30, 0), new TimeSpan(7, 30, 0), "Спокойная прогулка", (TimeSpan?)new TimeSpan(19, 0, 0), (short?)30)
        };
        foreach (var (name, description, bedtime, wakeUp, procedure, procedureTime, minutes) in definitions)
        {
            var program = new FitnessProgram { Kind = ProgramKind.Sleep, Name = name, Description = description, Weeks = 1, Image = "/images/covers/unavailable.svg", Active = true, CreatedAt = DateTime.UtcNow };
            program.Items.Add(new ProgramItem { Type = ProgramItemType.Bedtime, DayNumber = 1, Title = "Отбой", Time = bedtime, Order = 1 });
            program.Items.Add(new ProgramItem { Type = ProgramItemType.WakeUp, DayNumber = 1, Title = "Подъём", Time = wakeUp, Order = 2 });
            if (procedure is not null) program.Items.Add(new ProgramItem { Type = ProgramItemType.Procedure, DayNumber = 1, Title = procedure, Time = procedureTime, Minutes = minutes, Order = 3 });
            db.Programs.Add(program);
        }
        await db.SaveChangesAsync();
    }

    private static async Task EnsureBaseRecipesAsync(ApplicationDbContext db)
    {
        if (await db.FoodProducts.AnyAsync(x => x.OwnerId == null && x.Kind == FoodProductKind.Recipe)) return;
        var categoryId = await db.Categories.Where(x=>x.Section=="Продукты").OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var recipes = new[]
        {
            new FoodProduct { Id = 1000001, CategoryId = categoryId, Kind = FoodProductKind.Recipe, Name = "Овсяная каша с ягодами", Description = "Питательный завтрак с овсянкой и фруктами.", ServingGrams = 320, Calories = 100, Protein = 3.75m, Fat = 2.5m, Carbs = 16.875m, PreparationMinutes = 15, Instructions = "Сварите хлопья, добавьте банан и ягоды.", Image = "/images/food_oatmeal.jpg", Active = true },
            new FoodProduct { Id = 1000002, CategoryId = categoryId, Kind = FoodProductKind.Recipe, Name = "Омлет с овощами", Description = "Быстрый белковый завтрак.", ServingGrams = 250, Calories = 112, Protein = 7.2m, Fat = 8, Carbs = 3.2m, PreparationMinutes = 12, Instructions = "Взбейте яйца, добавьте овощи и приготовьте под крышкой.", Image = "/images/food_omelet.jpg", Active = true },
            new FoodProduct { Id = 1000003, CategoryId = categoryId, Kind = FoodProductKind.Recipe, Name = "Гречка с индейкой", Description = "Сбалансированный обед.", ServingGrams = 420, Calories = 100, Protein = 8.5714m, Fat = 2.8571m, Carbs = 11.4286m, PreparationMinutes = 35, Instructions = "Отварите гречку и приготовьте индейку с овощами.", Image = "/images/food_buckwheat.jpg", Active = true },
            new FoodProduct { Id = 1000004, CategoryId = categoryId, Kind = FoodProductKind.Recipe, Name = "Протеиновый смузи с бананом", Description = "Перекус после тренировки.", ServingGrams = 350, Calories = 71.4286m, Protein = 5.7143m, Fat = 1.7143m, Carbs = 12, PreparationMinutes = 5, Instructions = "Смешайте ингредиенты в блендере.", Image = "/images/food_smoothie.jpg", Active = true }
        };
        foreach (var recipe in recipes) recipe.Id = 0;
        db.FoodProducts.AddRange(recipes);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureMainMeasurementTypesAsync(ApplicationDbContext db)
    {
        var names = new[] { "Вес", "Талия", "Грудь", "Бёдра", "Бицепс", "Процент жира" };
        var types = await db.MeasurementTypes.ToListAsync();
        var changed = false;
        foreach (var type in types)
        {
            var isMain = names.Contains(type.Name);
            if (type.IsMain == isMain) continue;
            type.IsMain = isMain; changed = true;
        }
        if (changed) await db.SaveChangesAsync();
    }
}
