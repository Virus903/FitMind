using System.Net;
using System.Text.RegularExpressions;
using FitMind.Data;
using FitMind.Models;
using FitMind.Models.Identity;
using FitMind.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FitMind.Tests;

[CollectionDefinition("Account flow", DisableParallelization = true)]
public sealed class AccountFlowCollection : ICollectionFixture<FitMindFactory>;

[Collection("Account flow")]
public sealed class AccountFlowTests(FitMindFactory factory)
{
    [Fact]
    public async Task Register_creates_identity_user_and_profile()
    {
        var email = $"register-{Guid.NewGuid():N}@example.test";
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await PostFormAsync(client, "/Account/Register", new Dictionary<string, string>
        {
            ["Name"] = "Тестовый пользователь",
            ["Email"] = email,
            ["Password"] = "secure-pass-123",
            ["ConfirmPassword"] = "secure-pass-123"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Profile/Setup", response.Headers.Location?.OriginalString);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.UserProfiles.AnyAsync(x => x.UserId == user!.Id));
    }

    [Fact]
    public async Task Forgot_password_is_generic_and_sends_token_link_for_known_user()
    {
        var email = $"forgot-{Guid.NewGuid():N}@example.test";
        await factory.CreateCompletedUserAsync(email, "old-password-123");
        factory.EmailSender.Reset();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var known = await PostFormAsync(client, "/Account/ForgotPassword", new() { ["Email"] = email });
        var unknown = await PostFormAsync(client, "/Account/ForgotPassword", new() { ["Email"] = $"missing-{Guid.NewGuid():N}@example.test" });
        var knownBody = WebUtility.HtmlDecode(await known.Content.ReadAsStringAsync());
        var unknownBody = WebUtility.HtmlDecode(await unknown.Content.ReadAsStringAsync());
        const string genericText = "Если аккаунт существует и почтовая служба доступна";
        Assert.Contains(genericText, knownBody);
        Assert.Contains(genericText, unknownBody);
        Assert.Contains("ResetPassword", factory.EmailSender.LastHtmlBody);
    }

    [Fact]
    public async Task Reset_password_token_changes_password()
    {
        var email = $"reset-{Guid.NewGuid():N}@example.test";
        await factory.CreateCompletedUserAsync(email, "old-password-123");
        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            token = await users.GeneratePasswordResetTokenAsync(user!);
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var path = $"/Account/ResetPassword?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        var response = await PostFormAsync(client, path, new()
        {
            ["Email"] = email,
            ["Token"] = token,
            ["Password"] = "new-password-456",
            ["ConfirmPassword"] = "new-password-456"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.OriginalString);
        using var verifyScope = factory.Services.CreateScope();
        var verifyUsers = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updatedUser = await verifyUsers.FindByEmailAsync(email);
        Assert.True(await verifyUsers.CheckPasswordAsync(updatedUser!, "new-password-456"));
    }

    [Fact]
    public async Task Login_rejects_external_return_url()
    {
        var email = $"redirect-{Guid.NewGuid():N}@example.test";
        await factory.CreateCompletedUserAsync(email, "valid-password-123");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await PostFormAsync(client, "/Account/Login", new()
        {
            ["Email"] = email,
            ["Password"] = "valid-password-123",
            ["RememberMe"] = "false",
            ["returnUrl"] = "https://evil.example/steal"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Schedule", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Repeated_wrong_passwords_lock_the_account()
    {
        var email = $"lockout-{Guid.NewGuid():N}@example.test";
        await factory.CreateCompletedUserAsync(email, "valid-password-123");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            response = await PostFormAsync(client, "/Account/Login", new()
            {
                ["Email"] = email,
                ["Password"] = "wrong-password",
                ["RememberMe"] = "false"
            });
        }

        var body = WebUtility.HtmlDecode(await response!.Content.ReadAsStringAsync());
        Assert.Contains("Аккаунт временно заблокирован", body);
    }

    [Fact]
    public async Task Logout_invalidates_authenticated_session()
    {
        var email = $"logout-{Guid.NewGuid():N}@example.test";
        await factory.CreateCompletedUserAsync(email, "valid-password-123");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await PostFormAsync(client, "/Account/Login", new()
        {
            ["Email"] = email,
            ["Password"] = "valid-password-123",
            ["RememberMe"] = "false"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var schedule = await client.GetAsync("/Schedule");
        schedule.EnsureSuccessStatusCode();
        var token = ReadAntiforgeryToken(await schedule.Content.ReadAsStringAsync());
        var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        var protectedPage = await client.GetAsync("/Schedule");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        Assert.Equal("/Account/Login", protectedPage.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Settings_persists_light_theme_and_layout_renders_it()
    {
        var user = await factory.CreateCompletedUserAsync($"theme-{Guid.NewGuid():N}@example.test", "valid-password-123");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await PostFormAsync(client, "/Account/Login", new()
        {
            ["Email"] = user.Email!,
            ["Password"] = "valid-password-123",
            ["RememberMe"] = "false"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var save = await PostFormAsync(client, "/Settings", new()
        {
            ["notificationsEnabled"] = "true",
            ["emailNotifications"] = "false",
            ["quietHoursEnabled"] = "false",
            ["units"] = "Метрические",
            ["theme"] = "Светлая"
        });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("Светлая", await db.UserProfiles.Where(x => x.UserId == user.Id).Select(x => x.Theme).SingleAsync());
        }
        var settings = await client.GetAsync("/Settings");
        settings.EnsureSuccessStatusCode();
        Assert.Contains("data-theme=\"light\"", await settings.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Step10_screens_and_json_export_are_available_to_owner()
    {
        var user = await factory.CreateCompletedUserAsync($"step10-{Guid.NewGuid():N}@example.test", "valid-password-123");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Redirect, (await PostFormAsync(client, "/Account/Login", new() { ["Email"] = user.Email!, ["Password"] = "valid-password-123", ["RememberMe"] = "false" })).StatusCode);
        foreach (var path in new[] { "/Measurements", "/Calculators", "/Profile", "/Profile/Edit", "/Settings", "/Settings/Account" })
            (await client.GetAsync(path)).EnsureSuccessStatusCode();
        var export = await client.GetAsync("/Settings/ExportData"); export.EnsureSuccessStatusCode();
        Assert.Equal("application/json", export.Content.Headers.ContentType?.MediaType);
        Assert.Contains(user.Email!, await export.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(6, await db.MeasurementTypes.CountAsync(x => x.IsMain));
    }

    [Fact]
    public async Task Account_deletion_requires_password_and_removes_owned_data()
    {
        var user = await factory.CreateCompletedUserAsync($"delete-{Guid.NewGuid():N}@example.test", "valid-password-123");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.PersonalNotes.Add(new PersonalNote { UserId = user.Id, Area = "Тест", Text = "Удалить", CreatedAt = DateTime.Now }); await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Redirect, (await PostFormAsync(client, "/Account/Login", new() { ["Email"] = user.Email!, ["Password"] = "valid-password-123", ["RememberMe"] = "false" })).StatusCode);
        var deleted = await PostFormAsync(client, "/Settings/Account", new() { ["password"] = "valid-password-123", ["confirmation"] = "УДАЛИТЬ" }, "/Settings/DeleteAccount");
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        using var verify = factory.Services.CreateScope(); var users = verify.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByIdAsync(user.Id));
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string path,
        Dictionary<string, string> values,
        string? postPath = null)
    {
        var get = await client.GetAsync(path);
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();
        values["__RequestVerificationToken"] = ReadAntiforgeryToken(html);
        return await client.PostAsync(postPath ?? path, new FormUrlEncodedContent(values));
    }

    private static string ReadAntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}

[Collection("Account flow")]
public sealed class WorkoutScheduleTests(FitMindFactory factory)
{
    static async Task<(FitnessProgram Program, List<ProgramItem> Items)> CreateProgramAsync(ApplicationDbContext db, string userId, DateTime start, byte weeks, params (string Title, DayOfWeek Day, TimeSpan Time, int ExerciseId, byte Sets)[] days)
    {
        var program = new FitnessProgram { Kind = ProgramKind.Workout, OwnerId = userId, Name = "Тестовая программа", Active = true, StartDate = start, Weeks = weeks, EndDate = start.AddDays(weeks * 7 - 1), CreatedAt = DateTime.Now };
        byte number = 1;
        foreach (var d in days)
        {
            var item = new ProgramItem { DayNumber = number, DayOfWeek = d.Day, Time = d.Time, Type = ProgramItemType.Workout, Title = d.Title, Order = number++ };
            item.Exercises.Add(new ProgramItemExercise { ExerciseId = d.ExerciseId, Order = 1, Sets = d.Sets, RepsMin = 12, RepsMax = 12 });
            program.Items.Add(item);
        }
        db.Programs.Add(program);
        await db.SaveChangesAsync();
        return (program, program.Items.ToList());
    }

    [Fact]
    public async Task Three_program_days_create_distinct_workouts_for_their_weekday_slots()
    {
        var user = await factory.CreateCompletedUserAsync($"three-days-{Guid.NewGuid():N}@example.test", "valid-password-123");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category { Section = "Упражнения", Name = "Тест 3 дней" }; db.Categories.Add(category); await db.SaveChangesAsync();
        var exercises = new[] { "День А", "День Б", "День В" }.Select(name => new Exercise { CategoryId = category.Id, Name = name, Difficulty = "Начальный", Active = true }).ToList();
        db.Exercises.AddRange(exercises); await db.SaveChangesAsync();
        var monday = new DateTime(2030, 1, 7);
        var (program, _) = await CreateProgramAsync(db, user.Id, monday, 1,
            ("День 1", DayOfWeek.Monday, new TimeSpan(18, 0, 0), exercises[0].Id, 2),
            ("День 2", DayOfWeek.Tuesday, new TimeSpan(19, 0, 0), exercises[1].Id, 2),
            ("День 3", DayOfWeek.Wednesday, new TimeSpan(20, 0, 0), exercises[2].Id, 2));
        Assert.Equal(3, await ProgramScheduleService.PlanAsync(db, program));
        for (var index = 0; index < 3; index++) await ScheduleSyncService.SyncAsync(db, user.Id, monday.AddDays(index));
        var workouts = await db.DayEntries.Include(x => x.Sets).Where(x => x.UserId == user.Id).OrderBy(x => x.Start).ToListAsync();
        Assert.Equal(3, workouts.Count);
        Assert.Equal(new[] { "День 1", "День 2", "День 3" }, workouts.Select(x => x.Title));
        foreach (var w in workouts) await ProgramScheduleService.EnsureSetsAsync(db, w);
        var names = await db.DayEntries.Where(x => x.UserId == user.Id).OrderBy(x => x.Start).Select(x => x.Sets.Select(s => s.Exercise!.Name).First()).ToListAsync();
        Assert.Equal(new[] { "День А", "День Б", "День В" }, names);
        Assert.Equal(3, await db.DayEntries.CountAsync(x => x.UserId == user.Id && x.Type == ProgramItemType.Workout));
    }

    [Fact]
    public async Task Active_program_slot_creates_one_owned_workout_and_schedule_item()
    {
        var first = await factory.CreateCompletedUserAsync($"schedule-a-{Guid.NewGuid():N}@example.test", "valid-password-123");
        var second = await factory.CreateCompletedUserAsync($"schedule-b-{Guid.NewGuid():N}@example.test", "valid-password-123");
        var day = new DateTime(2030, 1, 7); // понедельник
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category { Section = "Упражнения", Name = "Тест" }; db.Categories.Add(category); await db.SaveChangesAsync();
        var exercise = new Exercise { CategoryId = category.Id, Name = "Тестовое упражнение", Difficulty = "Начальный", Active = true };
        db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var (program, items) = await CreateProgramAsync(db, first.Id, day, 1, ("Силовой день", DayOfWeek.Monday, new TimeSpan(18, 30, 0), exercise.Id, 3));

        await ProgramScheduleService.PlanAsync(db, program);
        await ProgramScheduleService.PlanAsync(db, program);
        await ScheduleSyncService.SyncAsync(db, first.Id, day);
        await ScheduleSyncService.SyncAsync(db, first.Id, day);

        var workouts = await db.DayEntries.Include(x => x.Sets).Where(x => x.UserId == first.Id && x.ProgramItemId == items[0].Id).ToListAsync();
        var workout = Assert.Single(workouts);
        Assert.Equal(DayEntryStatus.Planned, workout.Status);
        Assert.Equal(day.Add(new TimeSpan(18, 30, 0)), workout.Start);
        await ProgramScheduleService.EnsureSetsAsync(db, workout);
        Assert.Equal(3, workout.Sets.Count);
        Assert.Single(await db.DayEntries.Where(x => x.UserId == first.Id && x.Id == workout.Id).ToListAsync());
        Assert.Empty(await db.DayEntries.Where(x => x.UserId == second.Id).ToListAsync());

        // Выключение программы убирает будущие запланированные записи и их пункты «Моего дня».
        program.Active = false; await db.SaveChangesAsync();
        Assert.Equal(1, await ProgramScheduleService.UnplanFutureAsync(db, program));
        Assert.Empty(await db.DayEntries.Where(x => x.UserId == first.Id).ToListAsync());
        Assert.Empty(await db.DayEntries.Where(x => x.UserId == first.Id && x.Type == ProgramItemType.Workout).ToListAsync());
    }

    [Fact]
    public async Task Program_editor_to_my_day_to_completed_history_works_end_to_end()
    {
        var user=await factory.CreateCompletedUserAsync($"workout-e2e-{Guid.NewGuid():N}@example.test","valid-password-123");
        var other=await factory.CreateCompletedUserAsync($"workout-other-{Guid.NewGuid():N}@example.test","valid-password-123");
        int[] exerciseIds;
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category=new Category{Section="Упражнения",Name="E2E категория"};db.Categories.Add(category);await db.SaveChangesAsync();
            var exercises=Enumerable.Range(1,3).Select(i=>new Exercise{CategoryId=category.Id,Name=$"E2E упражнение {i}",Difficulty="Начальный",Active=true}).ToList();
            db.Exercises.AddRange(exercises);await db.SaveChangesAsync();exerciseIds=exercises.Select(x=>x.Id).ToArray();
        }
        using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
        await LoginForWorkoutAsync(client,user.Email!,"valid-password-123");
        // Своё упражнение из базы: появляется в «Моих упражнениях» и доступно в программе.
        Assert.Equal(HttpStatusCode.Redirect,(await WorkoutPostAsync(client,"/MyWorkouts",new(){{"id",exerciseIds[2].ToString()}},"/MyWorkouts/AddExerciseFromBase")).StatusCode);
        int myExerciseId;
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();myExerciseId=await db.Exercises.Where(x=>x.OwnerId==user.Id&&x.SourceExerciseId==exerciseIds[2]).Select(x=>x.Id).SingleAsync();}
        var values=new Dictionary<string,string>{{"Name","E2E программа"},{"Description","Три разных дня"},{"Weeks","4"},{"StartDate","2030-01-07"}};
        for(var i=0;i<3;i++)
        {
            values[$"Days[{i}].Name"]=$"E2E день {i+1}";values[$"Days[{i}].DayOfWeek"]=(i+1).ToString();values[$"Days[{i}].Time"]=$"{18+i}:00";
            values[$"Days[{i}].Exercises[0].ExerciseId"]=(i==2?myExerciseId:exerciseIds[i]).ToString();values[$"Days[{i}].Exercises[0].Sets"]="2";values[$"Days[{i}].Exercises[0].Reps"]=(8+i).ToString();values[$"Days[{i}].Exercises[0].RestSeconds"]="60";
        }
        var create=await WorkoutPostAsync(client,"/MyWorkouts/CreateProgram",values,"/MyWorkouts/CreateProgram");
        Assert.Equal(HttpStatusCode.Redirect,create.StatusCode);
        int programId;
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();programId=await db.Programs.Where(x=>x.OwnerId==user.Id&&x.Name=="E2E программа").Select(x=>x.Id).SingleAsync();
            var items=await db.ProgramItems.Where(x=>x.ProgramId==programId).Include(x=>x.Exercises).OrderBy(x=>x.DayNumber).ToListAsync();
            Assert.Equal(3,items.Count);Assert.All(items,x=>Assert.Single(x.Exercises));Assert.All(items,x=>Assert.NotNull(x.Time));
            Assert.Equal(myExerciseId,items[2].Exercises.Single().ExerciseId);
        }
        Assert.Contains("E2E программа",WebUtility.HtmlDecode(await client.GetStringAsync($"/MyWorkouts/ViewProgram/{programId}")));
        var activate=await WorkoutPostAsync(client,"/MyWorkouts",new(){{"id",programId.ToString()},{"active","true"}},"/MyWorkouts/SetProgramActive");
        Assert.Equal(HttpStatusCode.Redirect,activate.StatusCode);
        var schedule=await client.GetAsync("/Schedule?date=2030-01-07");schedule.EnsureSuccessStatusCode();
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(12,await db.DayEntries.CountAsync(x=>x.UserId==user.Id&&x.Status==DayEntryStatus.Planned));
            Assert.True(await db.DayEntries.AnyAsync(x=>x.UserId==user.Id&&x.Start.Date==new DateTime(2030,1,7)&&x.Type==ProgramItemType.Workout&&x.Title=="E2E день 1"));
        }
        Assert.Contains("E2E день 1",WebUtility.HtmlDecode(await schedule.Content.ReadAsStringAsync()));
        Assert.Contains("E2E день 1",WebUtility.HtmlDecode(await client.GetStringAsync("/MyWorkouts?month=2030-01-01")));
        long workoutId;
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();workoutId=await db.DayEntries.Where(x=>x.UserId==user.Id&&x.Start.Date==new DateTime(2030,1,7)).Select(x=>x.Id).SingleAsync();
        }
        (await client.GetAsync($"/MyWorkouts/Run/{workoutId}")).EnsureSuccessStatusCode();
        long setId;
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var sets=await db.EntrySets.Where(x=>x.DayEntryId==workoutId).OrderBy(x=>x.Number).ToListAsync();Assert.Equal(2,sets.Count);setId=sets[0].Id;}
        Assert.Equal(HttpStatusCode.Redirect,(await WorkoutPostAsync(client,$"/MyWorkouts/Run/{workoutId}",new(){{"id",workoutId.ToString()}},"/MyWorkouts/Begin")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await WorkoutPostAsync(client,$"/MyWorkouts/Run/{workoutId}",new(){{"setId",setId.ToString()},{"weightKg","40"},{"reps","10"},{"completed","true"}},"/MyWorkouts/SaveSet")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await WorkoutPostAsync(client,$"/MyWorkouts/Run/{workoutId}",new(){{"id",workoutId.ToString()},{"wellbeing","4"},{"note","Выполнено E2E"}},"/MyWorkouts/Finish")).StatusCode);
        var history=await client.GetAsync("/MyWorkouts/History");history.EnsureSuccessStatusCode();Assert.Contains("E2E день 1",WebUtility.HtmlDecode(await history.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect,(await WorkoutPostAsync(client,"/MyWorkouts",new(){{"id",programId.ToString()},{"active","false"}},"/MyWorkouts/SetProgramActive")).StatusCode);
        var historyAfterDeactivation=await client.GetAsync("/MyWorkouts/History");historyAfterDeactivation.EnsureSuccessStatusCode();Assert.Contains("E2E день 1",WebUtility.HtmlDecode(await historyAfterDeactivation.Content.ReadAsStringAsync()));
        using(var otherClient=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false}))
        {
            await LoginForWorkoutAsync(otherClient,other.Email!,"valid-password-123");Assert.Equal(HttpStatusCode.NotFound,(await otherClient.GetAsync($"/MyWorkouts/Run/{workoutId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await otherClient.GetAsync($"/MyWorkouts/ViewProgram/{programId}")).StatusCode);
        }
        using var verify=factory.Services.CreateScope();var verifyDb=verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await verifyDb.DayEntries.AnyAsync(x=>x.Id==workoutId&&x.Status==DayEntryStatus.Done&&x.Rating==4));
        Assert.True(await verifyDb.EntrySets.AnyAsync(x=>x.Id==setId&&x.Done&&x.WeightKg==40&&x.Reps==10));
        Assert.True(await verifyDb.DayEntries.AnyAsync(x=>x.UserId==user.Id&&x.Id==workoutId&&x.Status==DayEntryStatus.Done));
        // После выключения будущие запланированные записи удалены, выполненная осталась.
        Assert.Equal(1,await verifyDb.DayEntries.CountAsync(x=>x.UserId==user.Id));
    }

    [Fact]
    public async Task Editing_program_keeps_completed_workout_in_history()
    {
        var user = await factory.CreateCompletedUserAsync($"program-history-{Guid.NewGuid():N}@example.test", "valid-password-123");
        int programId, exerciseId; long workoutId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category = new Category { Section = "Упражнения", Name = $"История категории {Guid.NewGuid():N}" }; db.Categories.Add(category); await db.SaveChangesAsync();
            var exercise = new Exercise { CategoryId = category.Id, Name = "Упражнение для истории", Difficulty = "Начальный", Active = true };
            db.Exercises.Add(exercise); await db.SaveChangesAsync(); exerciseId = exercise.Id;
            var (program, items) = await CreateProgramAsync(db, user.Id, DateTime.Today.AddDays(-7), 4, ("Старый день", DayOfWeek.Monday, new TimeSpan(18, 0, 0), exerciseId, 3));
            program.Active = false; programId = program.Id;
            var workout = new DayEntry { UserId = user.Id, Type = ProgramItemType.Workout, ProgramItemId = items[0].Id, Title = "Завершённая тренировка", Start = DateTime.Now.AddDays(-2), End = DateTime.Now.AddDays(-2).AddHours(1), Status = DayEntryStatus.Done };
            db.DayEntries.Add(workout); await db.SaveChangesAsync(); workoutId = workout.Id;
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginForWorkoutAsync(client, user.Email!, "valid-password-123");
        var response = await WorkoutPostAsync(client, "/MyWorkouts/CreateProgram", new Dictionary<string, string>
        {
            ["Id"] = programId.ToString(), ["Name"] = "Обновлённая программа", ["Weeks"] = "4", ["StartDate"] = "2030-01-07",
            ["Days[0].Name"] = "Новый день", ["Days[0].DayOfWeek"] = "1", ["Days[0].Time"] = "18:00",
            ["Days[0].Exercises[0].ExerciseId"] = exerciseId.ToString(), ["Days[0].Exercises[0].Sets"] = "3", ["Days[0].Exercises[0].Reps"] = "10", ["Days[0].Exercises[0].RestSeconds"] = "60"
        }, "/MyWorkouts/CreateProgram");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await verifyDb.DayEntries.SingleAsync(x => x.Id == workoutId);
        Assert.Equal(DayEntryStatus.Done, saved.Status);
        Assert.Null(saved.ProgramItemId);
        Assert.Equal("Обновлённая программа", await verifyDb.Programs.Where(x => x.Id == programId).Select(x => x.Name).SingleAsync());
        Assert.Single(await verifyDb.ProgramItems.Where(x => x.ProgramId == programId).ToListAsync());
    }

    [Fact]
    public async Task Deleting_program_schedule_item_keeps_it_suppressed_after_sync()
    {
        var user = await factory.CreateCompletedUserAsync($"schedule-delete-{Guid.NewGuid():N}@example.test", "valid-password-123");
        var day = DateTime.Today.AddDays(2);
        long entryId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var program = new FitnessProgram { Kind = ProgramKind.Nutrition, OwnerId = user.Id, Name = "Удаляемый план", Active = true, StartDate = day, EndDate = day.AddDays(7), CreatedAt = DateTime.UtcNow };
            var item = new ProgramItem { Type = ProgramItemType.Meal, DayNumber = 1, Time = new TimeSpan(12, 0, 0), Title = "Пункт для удаления", Order = 1 };
            program.Items.Add(item); db.Programs.Add(program); await db.SaveChangesAsync();
            var entry = new DayEntry { UserId = user.Id, ProgramItemId = item.Id, Type = ProgramItemType.Meal, Start = day.AddHours(12), Title = item.Title, Status = DayEntryStatus.Planned };
            db.DayEntries.Add(entry); await db.SaveChangesAsync(); entryId = entry.Id;
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginForWorkoutAsync(client, user.Email!, "valid-password-123");
        var response = await WorkoutPostAsync(client, $"/Schedule?date={day:yyyy-MM-dd}", new() { ["id"] = entryId.ToString() }, "/Schedule/Delete");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await ScheduleSyncService.SyncAsync(verifyDb, user.Id, day);
        var entryAfterSync = await verifyDb.DayEntries.SingleAsync(x => x.Id == entryId);
        Assert.Equal(DayEntryStatus.Skipped, entryAfterSync.Status);
        var schedule = WebUtility.HtmlDecode(await client.GetStringAsync($"/Schedule?date={day:yyyy-MM-dd}"));
        Assert.DoesNotContain("Пункт для удаления", schedule);
    }

    private static async Task LoginForWorkoutAsync(HttpClient client,string email,string password)
    {
        var response=await WorkoutPostAsync(client,"/Account/Login",new(){{"Email",email},{"Password",password},{"RememberMe","false"}},"/Account/Login");Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
    }
    private static async Task<HttpResponseMessage> WorkoutPostAsync(HttpClient client,string tokenPage,Dictionary<string,string> values,string postUrl)
    {
        var get=await client.GetAsync(tokenPage);get.EnsureSuccessStatusCode();var html=await get.Content.ReadAsStringAsync();var match=Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");Assert.True(match.Success);values["__RequestVerificationToken"]=WebUtility.HtmlDecode(match.Groups[1].Value);return await client.PostAsync(postUrl,new FormUrlEncodedContent(values));
    }
}

public sealed class FitMindFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"FitMindTests_{Guid.NewGuid():N}";
    private bool databaseDeleted;
    public string TestDatabaseName => databaseName;
    // Отдельная база на каждый запуск тестов. Сервер берётся из ConnectionStrings__DefaultConnection,
    // если переменная задана, иначе LocalDB; имя базы всегда тестовое.
    private string TestConnectionString => new SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=FitMind;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
    {
        InitialCatalog = databaseName
    }.ConnectionString;
    public CapturingEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["DemoAccount:Enabled"] = "false",
                ["Email:Smtp:Enabled"] = "false"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options=>options.UseSqlServer(TestConnectionString));
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);
        });
    }

    public async Task<ApplicationUser> CreateCompletedUserAsync(string email, string password)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        var roleResult = await users.AddToRoleAsync(user, "Пользователь");
        Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(x => x.Description)));
        db.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            Name = "Integration Test",
            CreatedAt = DateTime.UtcNow,
            Gender = "Не указан",
            OnboardingStep = 4,
            OnboardingCompleted = true
        });
        await db.SaveChangesAsync();
        return user;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !databaseDeleted)
        {
            databaseDeleted = true;
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeleted();
        }
        base.Dispose(disposing);
    }
}

public sealed class CapturingEmailSender : IEmailSender
{
    public string LastHtmlBody { get; private set; } = "";
    public Task<bool> SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        LastHtmlBody = htmlBody;
        return Task.FromResult(true);
    }
    public void Reset() => LastHtmlBody = "";
}
