using System.Net;
using System.Text.RegularExpressions;
using FitMind.Data;
using FitMind.Models;
using FitMind.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FitMind.Areas.Admin.Controllers;

namespace FitMind.Tests;

[Collection("Account flow")]
public sealed class AdminTests(FitMindFactory factory)
{
    [Fact]
    public void Media_signatures_accept_supported_formats_and_reject_fake_file()
    {
        var png=new byte[24];new byte[]{137,80,78,71,13,10,26,10}.CopyTo(png,0);System.Text.Encoding.ASCII.GetBytes("IHDR").CopyTo(png,12);
        var jpeg=new byte[24];jpeg[0]=255;jpeg[1]=216;jpeg[2]=255;jpeg[^2]=255;jpeg[^1]=217;
        var webp=new byte[24];System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(webp,0);System.Text.Encoding.ASCII.GetBytes("WEBP").CopyTo(webp,8);
        Assert.Equal(".png",MediaController.DetectExtension(png));
        Assert.Equal(".jpg",MediaController.DetectExtension(jpeg));
        Assert.Equal(".webp",MediaController.DetectExtension(webp));
        Assert.Null(MediaController.DetectExtension(new byte[24]));
    }

    [Fact]
    public async Task Administrator_changes_role_and_lock_without_affecting_self()
    {
        var admin=await factory.CreateCompletedUserAsync($"role-admin-{Guid.NewGuid():N}@example.test","valid-password-123");
        var target=await factory.CreateCompletedUserAsync($"role-target-{Guid.NewGuid():N}@example.test","valid-password-123");
        using(var scope=factory.Services.CreateScope())
        {
            var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await manager.AddToRoleAsync((await manager.FindByIdAsync(admin.Id))!,"Администратор")).Succeeded);
        }
        using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
        await LoginAsync(client,admin.Email!,"valid-password-123");
        var role=await PostAsync(client,$"/Admin/Admin/UserDetails?id={target.Id}",new(){["id"]=target.Id},"/Admin/Admin/ToggleAdminRole");
        Assert.Equal(HttpStatusCode.Redirect,role.StatusCode);
        var lockResult=await PostAsync(client,$"/Admin/Admin/UserDetails?id={target.Id}",new(){["id"]=target.Id},"/Admin/Admin/ToggleLock");
        Assert.Equal(HttpStatusCode.Redirect,lockResult.StatusCode);
        var selfLock=await PostAsync(client,$"/Admin/Admin/UserDetails?id={admin.Id}",new(){["id"]=admin.Id},"/Admin/Admin/ToggleLock");
        Assert.Equal(HttpStatusCode.Redirect,selfLock.StatusCode);
        using var verify=factory.Services.CreateScope();var users=verify.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updatedTarget=await users.FindByIdAsync(target.Id);var updatedAdmin=await users.FindByIdAsync(admin.Id);
        Assert.True(await users.IsInRoleAsync(updatedTarget!,"Администратор"));
        Assert.True(updatedTarget!.LockoutEnd>DateTimeOffset.UtcNow);
        Assert.False(updatedAdmin!.LockoutEnd>DateTimeOffset.UtcNow);
    }
    [Fact]
    public async Task Admin_routes_reject_regular_user_and_allow_administrator()
    {
        var regular = await factory.CreateCompletedUserAsync($"regular-{Guid.NewGuid():N}@example.test", "valid-password-123");
        var admin = await factory.CreateCompletedUserAsync($"admin-{Guid.NewGuid():N}@example.test", "valid-password-123");
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await manager.AddToRoleAsync((await manager.FindByIdAsync(admin.Id))!, "Администратор");
            Assert.True(result.Succeeded);
        }

        using var regularClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(regularClient, regular.Email!, "valid-password-123");
        var forbidden = await regularClient.GetAsync("/Admin/Admin");
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
        Assert.Equal("/Account/AccessDenied", forbidden.Headers.Location?.AbsolutePath);

        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(adminClient, admin.Email!, "valid-password-123");
        var allowed = await adminClient.GetAsync("/Admin/Admin");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/Admin/NutritionPrograms")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/Admin/SleepPrograms")).StatusCode);
    }

    [Fact]
    public async Task Administrator_creates_catalog_entry_and_audit_log()
    {
        var admin = await factory.CreateCompletedUserAsync($"content-admin-{Guid.NewGuid():N}@example.test", "valid-password-123");
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await manager.AddToRoleAsync((await manager.FindByIdAsync(admin.Id))!, "Администратор")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Categories.Add(new Category { Section = "Упражнения", Name = "Admin test category" });
            db.MuscleGroups.Add(new MuscleGroup { Name = "Admin test muscle" });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, admin.Email!, "valid-password-123");
        using var scope2 = factory.Services.CreateScope();
        var seedDb = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var categoryId = await seedDb.Categories.Where(x => x.Section == "Упражнения" && x.Name == "Admin test category").Select(x => x.Id).SingleAsync();
        var muscleId = await seedDb.MuscleGroups.Where(x => x.Name == "Admin test muscle").Select(x => x.Id).SingleAsync();
        var response = await PostAsync(client, "/Admin/Catalog/Create?kind=exercises", new()
        {
            ["Kind"] = "exercises", ["Name"] = "Admin exercise", ["Description"] = "Created by admin test", ["CategoryId"] = categoryId.ToString(), ["Active"] = "true", ["Meta"] = "Начальный",
            ["MuscleIds"] = muscleId.ToString(), ["PrimaryMuscleId"] = muscleId.ToString()
        }, "/Admin/Catalog/Save");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db2.Exercises.AnyAsync(x => x.Name == "Admin exercise" && x.Active));
        var exerciseId=await db2.Exercises.Where(x=>x.Name=="Admin exercise").Select(x=>x.Id).SingleAsync();
        Assert.True(await db2.ExerciseMuscles.AnyAsync(x=>x.ExerciseId==exerciseId&&x.MuscleGroupId==muscleId&&x.IsPrimary));
        Assert.True(await db2.AdminAuditLogs.AnyAsync(x => x.EntityType == "Упражнения" && x.Action == "Создание"));
    }

    [Fact]
    public async Task Administrator_builds_ready_program_with_day_and_exercise()
    {
        var marker = Guid.NewGuid().ToString("N");
        var admin = await factory.CreateCompletedUserAsync($"program-admin-{marker}@example.test", "valid-password-123");
        int goalId, levelId, exerciseId; string goalName;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(admin.Id))!, "Администратор")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var goal = new FitnessGoal { Name = $"Goal {marker}" };
            var level = new TrainingLevel { Name = $"Level {marker}" };
            var category = new Category { Section = "Упражнения", Name = $"Category {marker}" };
            db.AddRange(goal, level, category); await db.SaveChangesAsync();
            var exercise = new Exercise { Name = $"Exercise {marker}", CategoryId = category.Id, Difficulty = "Начальный", Active = true };
            db.Exercises.Add(exercise); await db.SaveChangesAsync();
            (goalId, levelId, exerciseId, goalName) = (goal.Id, level.Id, exercise.Id, goal.Name);
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, admin.Email!, "valid-password-123");
        var programName = $"Program {marker}";
        var saveProgram = await PostAsync(client, "/Admin/Programs/Edit", new()
        {
            ["id"]="0", ["name"]=programName, ["forWhom"]="Для начинающих", ["place"]="Зал", ["averageDurationMinutes"]="55",
            ["goalText"]=goalName, ["levelId"]=levelId.ToString(), ["weeks"]="6", ["workoutsPerWeek"]="3", ["active"]="true"
        }, "/Admin/Programs/Save");
        Assert.Equal(HttpStatusCode.Redirect, saveProgram.StatusCode);

        int programId;
        using (var scope = factory.Services.CreateScope())
            programId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Programs.Where(x=>x.Name==programName&&x.OwnerId==null).Select(x=>x.Id).SingleAsync();

        var saveDay = await PostAsync(client, $"/Admin/Programs/Edit/{programId}", new()
        {
            ["programId"]=programId.ToString(), ["id"]="0", ["name"]="День A", ["description"]="Описание дня", ["dayNumber"]="1"
        }, "/Admin/Programs/SaveDay");
        Assert.Equal(HttpStatusCode.Redirect, saveDay.StatusCode);

        int dayId;
        using (var scope = factory.Services.CreateScope())
            dayId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ProgramItems.Where(x=>x.ProgramId==programId).Select(x=>x.Id).SingleAsync();

        var saveExercise = await PostAsync(client, $"/Admin/Programs/Edit/{programId}", new()
        {
            ["programId"]=programId.ToString(), ["dayId"]=dayId.ToString(), ["id"]="0", ["exerciseId"]=exerciseId.ToString(),
            ["order"]="1", ["sets"]="4", ["repsMin"]="8", ["repsMax"]="12", ["restSeconds"]="90", ["comment"]="Контроль техники"
        }, "/Admin/Programs/SaveExercise");
        Assert.Equal(HttpStatusCode.Redirect, saveExercise.StatusCode);

        var duplicateOrder = await PostAsync(client, $"/Admin/Programs/Edit/{programId}", new()
        {
            ["programId"]=programId.ToString(), ["dayId"]=dayId.ToString(), ["id"]="0", ["exerciseId"]=exerciseId.ToString(),
            ["order"]="1", ["sets"]="3", ["repsMax"]="10"
        }, "/Admin/Programs/SaveExercise");
        Assert.Equal(HttpStatusCode.Redirect, duplicateOrder.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var savedProgram=await verifyDb.Programs.SingleAsync(x=>x.Id==programId);Assert.Equal(goalName,savedProgram.GoalText);
        Assert.Equal("Для начинающих",savedProgram.ForWhom);Assert.Equal("Зал",savedProgram.Place);Assert.Equal((short)55,savedProgram.AverageDurationMinutes);
        Assert.Equal("Описание дня",await verifyDb.ProgramItems.Where(x=>x.Id==dayId).Select(x=>x.Description).SingleAsync());
        Assert.Single(await verifyDb.ProgramItemExercises.Where(x=>x.ProgramItemId==dayId).ToListAsync());
        Assert.True(await verifyDb.ProgramItemExercises.AnyAsync(x=>x.ProgramItemId==dayId && x.ExerciseId==exerciseId && x.Sets==4 && x.Comment=="Контроль техники"));
        Assert.True(await verifyDb.AdminAuditLogs.CountAsync(x=>x.EntityType=="Программа" && x.EntityId==programId.ToString()) >= 3);
    }

    [Fact]
    public async Task Recipe_ingredient_is_editable_and_referenced_product_is_protected()
    {
        var marker = Guid.NewGuid().ToString("N");
        var admin = await factory.CreateCompletedUserAsync($"recipe-admin-{marker}@example.test", "valid-password-123");
        int categoryId, productId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(admin.Id))!, "Администратор")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category = new Category { Section = "Продукты", Name = $"Food {marker}" }; db.Categories.Add(category); await db.SaveChangesAsync();
            categoryId=category.Id;
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, admin.Email!, "valid-password-123");
        var productName=$"Product {marker}";
        var createProduct=await PostAsync(client,"/Admin/Catalog/Create?kind=products",new()
        {
            ["Kind"]="products",["Name"]=productName,["CategoryId"]=categoryId.ToString(),["Active"]="true",
            ["ServingGrams"]="100",["Calories"]="50",["Protein"]="12",["Fat"]="3",["Carbs"]="7",["Fiber"]="2",["GlycemicIndex"]="35"
        },"/Admin/Catalog/Save");
        Assert.Equal(HttpStatusCode.Redirect,createProduct.StatusCode);
        using(var scope=factory.Services.CreateScope()) productId=await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().FoodProducts.Where(x=>x.Name==productName).Select(x=>x.Id).SingleAsync();
        var recipeName=$"Recipe {marker}";
        var create=await PostAsync(client,"/Admin/Catalog/Create?kind=recipes",new()
        {
            ["Kind"]="recipes",["Name"]=recipeName,["CategoryId"]=categoryId.ToString(),["Active"]="true",["Meta"]="Обед",
            ["PreparationMinutes"]="25",["ServingGrams"]="300",["Calories"]="420",["Protein"]="30",["Fat"]="10",["Carbs"]="52",["Tips"]="Подавать тёплым"
        },"/Admin/Catalog/Save");
        Assert.Equal(HttpStatusCode.Redirect,create.StatusCode);
        int recipeId;
        using(var scope=factory.Services.CreateScope()) recipeId=await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().FoodProducts.Where(x=>x.Name==recipeName&&x.Kind==FoodProductKind.Recipe).Select(x=>x.Id).SingleAsync();
        var add=await PostAsync(client,$"/Admin/Catalog/Edit?kind=recipes&id={recipeId}",new()
        {
            ["recipeId"]=recipeId.ToString(),["productId"]=productId.ToString(),["grams"]="120"
        },"/Admin/Catalog/AddIngredient");
        Assert.Equal(HttpStatusCode.Redirect,add.StatusCode);
        int ingredientId;
        using(var scope=factory.Services.CreateScope()) ingredientId=await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RecipeIngredients.Where(x=>x.RecipeId==recipeId).Select(x=>x.Id).SingleAsync();
        var edit=await PostAsync(client,$"/Admin/Catalog/Edit?kind=recipes&id={recipeId}",new()
        {
            ["recipeId"]=recipeId.ToString(),["ingredientId"]=ingredientId.ToString(),["name"]="Обновлённый ингредиент",["grams"]="150",["order"]="2"
        },"/Admin/Catalog/EditIngredient");
        Assert.Equal(HttpStatusCode.Redirect,edit.StatusCode);
        var delete=await PostAsync(client,$"/Admin/Catalog/Delete?kind=products&id={productId}",new()
        {
            ["kind"]="products",["id"]=productId.ToString()
        },"/Admin/Catalog/Delete");
        Assert.Equal(HttpStatusCode.Redirect,delete.StatusCode);
        using var verify=factory.Services.CreateScope();var db2=verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db2.FoodProducts.AnyAsync(x=>x.Id==productId));
        Assert.True(await db2.FoodProducts.AnyAsync(x=>x.Id==productId&&x.Calories==50&&x.Protein==12&&x.Fat==3&&x.Carbs==7&&x.Fiber==2&&x.GlycemicIndex==35));
        Assert.True(await db2.FoodProducts.AnyAsync(x=>x.Id==recipeId&&x.Kind==FoodProductKind.Recipe&&x.PreparationMinutes==25&&x.Calories==420&&x.Protein==30&&x.Fat==10&&x.Carbs==52&&x.CookingTips=="Подавать тёплым"));
        Assert.True(await db2.RecipeIngredients.AnyAsync(x=>x.Id==ingredientId&&x.Name=="Обновлённый ингредиент"&&x.Grams==150&&x.Order==2));
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await PostAsync(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = password, ["RememberMe"] = "false" }, "/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string tokenPage, Dictionary<string, string> values, string postUrl)
    {
        var get = await client.GetAsync(tokenPage); get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success); values["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(values));
    }
}
