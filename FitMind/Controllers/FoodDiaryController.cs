using System.Security.Claims;
using FitMind.Data;
using FitMind.Models;
using FitMind.Services;
using FitMind.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Controllers;

[Authorize]
public class FoodDiaryController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
{
    string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    IQueryable<FitnessProgram> MyPlans => db.Programs.Where(x => x.OwnerId == Uid && x.Kind == ProgramKind.Nutrition);

    public async Task<IActionResult> Index(DateTime? date, DateTime? month)
    {
        var day = (date ?? DateTime.Today).Date;
        await EnsurePlanEntries(day);
        var start = month.HasValue ? new DateTime(month.Value.Year, month.Value.Month, 1) : new DateTime(day.Year, day.Month, 1);
        var entries = await db.DayEntries.Where(x => x.UserId == Uid && x.Start >= day && x.Start < day.AddDays(1) && (x.Type == ProgramItemType.Meal || x.Type == ProgramItemType.Water))
            .Include(x => x.FoodItems).ThenInclude(x => x.Product).OrderBy(x => x.Start).ToListAsync();
        var plans = await MyPlans.Include(x => x.Items).OrderByDescending(x => x.Active).ThenByDescending(x => x.CreatedAt).ToListAsync();
        var meals = entries.Where(x => x.Type == ProgramItemType.Meal && x.Status == DayEntryStatus.Done).ToList();
        return View(new FoodDiaryRedesignVm
        {
            Date = day, Month = start, WeekStart = day.AddDays(-(((int)day.DayOfWeek + 6) % 7)), Entries = entries,
            MarkedDates = await db.DayEntries.Where(x => x.UserId == Uid && x.Start >= start.AddDays(-7) && x.Start < start.AddMonths(1).AddDays(7) && (x.Type == ProgramItemType.Meal || x.Type == ProgramItemType.Water)).Select(x => x.Start.Date).Distinct().ToListAsync(),
            Plans = plans, ActivePlan = plans.FirstOrDefault(x => x.Active),
            MyFood = await db.FoodProducts.Where(x => x.OwnerId == Uid && x.Active).OrderBy(x => x.Name).ToListAsync(),
            Notes = await db.PersonalNotes.Where(x => x.UserId == Uid && x.Area == "Питание").OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync(),
            Calories = meals.Sum(x => x.FoodItems.Sum(ItemCalories)), Protein = meals.Sum(x => x.FoodItems.Sum(ItemProtein)), Fat = meals.Sum(x => x.FoodItems.Sum(ItemFat)), Carbs = meals.Sum(x => x.FoodItems.Sum(ItemCarbs)),
            WaterMl = (int)entries.Where(x => x.Type == ProgramItemType.Water && x.Status == DayEntryStatus.Done).Sum(x => x.Value ?? 0), WaterGoalMl = plans.FirstOrDefault(x => x.Active)?.WaterMl ?? 2000
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPlanActive(int id, bool active = true)
    {
        var plan = await MyPlans.FirstOrDefaultAsync(x => x.Id == id); if (plan is null) return NotFound();
        foreach (var other in await MyPlans.Where(x => x.Active && x.Id != id).ToListAsync())
        {
            other.Active = false;
            await ProgramScheduleService.UnplanFutureAsync(db, other);
        }
        plan.Active = active; await db.SaveChangesAsync();
        if (active) await EnsurePlanEntries(DateTime.Today);
        else await ProgramScheduleService.UnplanFutureAsync(db, plan);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreatePlan(int? id)
    {
        await FillProducts();
        if (id.HasValue)
        {
            var p = await MyPlans.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id); if (p is null) return NotFound();
            return View(ToPlanVm(p));
        }
        return View(NewPlanVm());
    }

    [HttpPost]
    public async Task<IActionResult> CreatePlan(NutritionPlanEditVm vm)
    {
        vm.Items ??= []; await FillProducts();
        var upload = await ContentImageStorage.SaveAsync(vm.ImageFile, environment);
        if (upload.Error != null) ModelState.AddModelError(nameof(vm.ImageFile), upload.Error);
        var meals = vm.Items.Take(vm.MealsPerDay).ToList();
        if (meals.Count != vm.MealsPerDay || meals.Any(x => string.IsNullOrWhiteSpace(x.Name) || !x.Time.HasValue)) ModelState.AddModelError("", "Заполните название и время каждого приёма пищи.");
        if (!ModelState.IsValid) return View(vm);
        FitnessProgram plan;
        if (vm.Id.HasValue) { plan = await MyPlans.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == vm.Id) ?? null!; if (plan is null) return NotFound(); db.ProgramItems.RemoveRange(plan.Items); }
        else { plan = new FitnessProgram { Kind = ProgramKind.Nutrition, OwnerId = Uid, CreatedAt = DateTime.UtcNow }; db.Programs.Add(plan); }
        plan.Name = vm.Name.Trim(); plan.Description = vm.Description?.Trim(); plan.GoalText = vm.Goal; plan.CaloriesPerDay = vm.CaloriesTarget; plan.WaterMl = vm.WaterMl; plan.Weeks = vm.Weeks; plan.StartDate = vm.StartDate.Date; plan.EndDate = vm.StartDate.Date.AddDays(vm.Weeks * 7); plan.Image = upload.Path ?? vm.Image ?? plan.Image ?? "/images/placeholders/nutrition-programs-dark.png";
        await db.SaveChangesAsync();
        short order = 1;
        foreach (var item in meals)
        {
            if (item.ProductId.HasValue && !await AvailableProducts().AnyAsync(x => x.Id == item.ProductId)) { ModelState.AddModelError("", "Выбранный продукт недоступен."); return View(vm); }
            db.ProgramItems.Add(new ProgramItem { ProgramId = plan.Id, Type = ProgramItemType.Meal, DayNumber = 1, DayOfWeek = null, Time = item.Time, Title = item.Name!.Trim(), ProductId = item.ProductId, Dose = item.Grams?.ToString(System.Globalization.CultureInfo.InvariantCulture), Calories = item.Calories, Order = order++ });
        }
        db.ProgramItems.Add(new ProgramItem { ProgramId = plan.Id, Type = ProgramItemType.Water, DayNumber = 1, Time = new TimeSpan(9, 0, 0), Title = "Вода", WaterMl = vm.WaterMl, Dose = vm.Description, Order = order });
        await db.SaveChangesAsync(); TempData["Ok"] = "Программа питания сохранена."; return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeletePlan(int id)
    {
        var plan = await MyPlans.FirstOrDefaultAsync(x => x.Id == id); if (plan is null) return NotFound(); db.Remove(plan); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    [HttpGet] public async Task<IActionResult> CreateFoodItem(int? id) { await FillCategories(); if (!id.HasValue) return View(new UserFoodItemEditVm()); var x = await db.FoodProducts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == Uid); return x is null ? NotFound() : View(ToFoodVm(x)); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFoodItem(UserFoodItemEditVm vm, int? id)
    {
        await FillCategories(); var upload = await ContentImageStorage.SaveAsync(vm.ImageFile, environment); if (upload.Error != null) ModelState.AddModelError(nameof(vm.ImageFile), upload.Error); if (!await db.Categories.AnyAsync(x => x.Section=="Продукты" && x.Id == vm.CategoryId)) ModelState.AddModelError(nameof(vm.CategoryId), "Выберите категорию."); if (!ModelState.IsValid) return View(vm);
        var item = id.HasValue ? await db.FoodProducts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == Uid) : new FoodProduct { OwnerId = Uid, Active = true };
        if (item is null) return NotFound(); if (!id.HasValue) db.FoodProducts.Add(item);
        item.Name = vm.Name.Trim(); item.Kind = vm.Kind; item.CategoryId = vm.CategoryId; item.Image = upload.Path ?? vm.Image ?? item.Image ?? "/images/placeholders/food-dark.png"; item.Calories = vm.Calories; item.Protein = vm.Protein; item.Fat = vm.Fat; item.Carbs = vm.Carbs; item.Description = vm.Description; item.Instructions = vm.Instructions; item.PreparationMinutes = vm.PreparationMinutes;
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddFoodFromBase(int id)
    {
        var source = await db.FoodProducts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == null && x.Active); if (source is null) return NotFound();
        var copy = await db.FoodProducts.FirstOrDefaultAsync(x => x.OwnerId == Uid && x.SourceId == id);
        if (copy is null) { copy = new FoodProduct { OwnerId = Uid, SourceId = id, Kind = source.Kind, CategoryId = source.CategoryId, Name = source.Name, Description = source.Description, Image = source.Image, ServingGrams = source.ServingGrams, Calories = source.Calories, Protein = source.Protein, Fat = source.Fat, Carbs = source.Carbs, Instructions = source.Instructions, PreparationMinutes = source.PreparationMinutes, Active = true }; db.Add(copy); await db.SaveChangesAsync(); }
        else if (!copy.Active) { copy.Active = true; await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFood(int id)
    {
        var item = await db.FoodProducts.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == Uid); if (item is null) return NotFound();
        if (await db.DayEntryFoodItems.AnyAsync(x => x.ProductId == id) || await db.ProgramItems.AnyAsync(x => x.ProductId == id)) item.Active = false; else db.Remove(item);
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> AddMeal(DateTime? date, ProgramItemType type = ProgramItemType.Meal, long? id = null, int? productId = null)
    {
        await FillProducts(); return View(new MealEditVm { EntryId = id, Type = type is ProgramItemType.Meal or ProgramItemType.Water ? type : ProgramItemType.Meal, Date = (date ?? DateTime.Today).Date, Time = DateTime.Now.TimeOfDay, ProductId = productId, Grams = 100 });
    }

    [HttpPost]
    public async Task<IActionResult> AddMeal(MealEditVm vm)
    {
        await FillProducts();
        if (vm.Type == ProgramItemType.Water && (vm.Grams < 1 || vm.Grams > 10000)) ModelState.AddModelError(nameof(vm.Grams), "Укажите объём от 1 до 10000 мл.");
        if (vm.Type == ProgramItemType.Meal && !vm.ProductId.HasValue && string.IsNullOrWhiteSpace(vm.CustomName)) ModelState.AddModelError(nameof(vm.CustomName), "Выберите продукт или введите название.");
        FoodProduct? product = null; if (vm.ProductId.HasValue) { product = await AvailableProducts().FirstOrDefaultAsync(x => x.Id == vm.ProductId); if (product is null) return NotFound(); }
        if (!ModelState.IsValid) return View(vm);
        var entry = new DayEntry { UserId = Uid, Type = vm.Type, Start = vm.Date.Date.Add(vm.Time), Title = vm.Type == ProgramItemType.Water ? (string.IsNullOrWhiteSpace(vm.Name) ? "Вода" : vm.Name.Trim()) : (string.IsNullOrWhiteSpace(vm.Name) ? "Приём пищи" : vm.Name.Trim()), Status = DayEntryStatus.Done, Value = vm.Type == ProgramItemType.Water ? vm.Grams : null, Comment = vm.Comment };
        db.DayEntries.Add(entry); await db.SaveChangesAsync();
        if (vm.Type == ProgramItemType.Meal) db.DayEntryFoodItems.Add(new DayEntryFoodItem { DayEntryId = entry.Id, ProductId = product?.Id, CustomName = product is null ? vm.CustomName?.Trim() : null, Grams = vm.Grams, CaloriesPer100 = product?.Calories ?? vm.CaloriesPer100, ProteinPer100 = product?.Protein ?? vm.ProteinPer100, FatPer100 = product?.Fat ?? vm.FatPer100, CarbsPer100 = product?.Carbs ?? vm.CarbsPer100 });
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index), new { date = vm.Date.ToString("yyyy-MM-dd") });
    }

    [HttpGet] public IActionResult AddWater(DateTime? date) => RedirectToAction(nameof(AddMeal), new { date = date?.ToString("yyyy-MM-dd"), type = ProgramItemType.Water });
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> DeleteEntry(long id) { var x = await db.DayEntries.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid && (x.Type == ProgramItemType.Meal || x.Type == ProgramItemType.Water)); if (x is null) return NotFound(); db.Remove(x); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    [HttpPost] public async Task<IActionResult> AddNote(string text, string? title = null) { if (!string.IsNullOrWhiteSpace(text)) { db.PersonalNotes.Add(new PersonalNote { UserId = Uid, Area = "Питание", Title = title, Text = text.Trim(), CreatedAt = DateTime.Now }); await db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> History() => View(await db.DayEntries.Where(x => x.UserId == Uid && (x.Type == ProgramItemType.Meal || x.Type == ProgramItemType.Water)).Include(x => x.FoodItems).ThenInclude(x => x.Product).OrderByDescending(x => x.Start).ToListAsync());

    async Task EnsurePlanEntries(DateTime day)
    {
        var plan = await MyPlans.Include(x => x.Items).FirstOrDefaultAsync(x => x.Active); if (plan is null) return;
        foreach (var item in plan.Items.Where(x => x.DayOfWeek == null || x.DayOfWeek == day.DayOfWeek))
        {
            var start = day.Add(item.Time ?? TimeSpan.Zero);
            if (!await db.DayEntries.AnyAsync(x => x.UserId == Uid && x.ProgramItemId == item.Id && x.Start == start)) db.DayEntries.Add(new DayEntry { UserId = Uid, ProgramItemId = item.Id, Type = item.Type, Start = start, Title = item.Title, Status = DayEntryStatus.Planned, Value = item.Type == ProgramItemType.Water ? item.WaterMl : null });
        }
        await db.SaveChangesAsync();
    }
    IQueryable<FoodProduct> AvailableProducts() => db.FoodProducts.Where(x => x.Active && (x.OwnerId == null || x.OwnerId == Uid));
    async Task FillProducts() => ViewBag.Products = await AvailableProducts().OrderByDescending(x => x.OwnerId == Uid).ThenBy(x => x.Name).ToListAsync();
    async Task FillCategories() => ViewBag.Categories = await db.Categories.Where(x=>x.Section=="Продукты").OrderBy(x => x.Name).ToListAsync();
    static decimal ItemCalories(DayEntryFoodItem x) => (x.CaloriesPer100 ?? x.Product?.Calories ?? 0) * x.Grams / 100;
    static decimal ItemProtein(DayEntryFoodItem x) => (x.ProteinPer100 ?? x.Product?.Protein ?? 0) * x.Grams / 100;
    static decimal ItemFat(DayEntryFoodItem x) => (x.FatPer100 ?? x.Product?.Fat ?? 0) * x.Grams / 100;
    static decimal ItemCarbs(DayEntryFoodItem x) => (x.CarbsPer100 ?? x.Product?.Carbs ?? 0) * x.Grams / 100;
    static NutritionPlanEditVm NewPlanVm() => new() { Items = [new() { Name = "Завтрак", Time = new(8, 0, 0) }, new() { Name = "Обед", Time = new(13, 0, 0) }, new() { Name = "Перекус", Time = new(16, 0, 0) }, new() { Name = "Ужин", Time = new(19, 0, 0) }, new() { Name = "Приём пищи", Time = new(11, 0, 0) }, new() { Name = "Приём пищи", Time = new(21, 0, 0) }] };
    static NutritionPlanEditVm ToPlanVm(FitnessProgram p) => new() { Id = p.Id, Name = p.Name, Description = p.Description, Goal = p.GoalText, Image = p.Image, CaloriesTarget = p.CaloriesPerDay, WaterMl = p.WaterMl ?? 2000, Weeks = p.Weeks ?? 4, StartDate = p.StartDate ?? DateTime.Today, MealsPerDay = (byte)p.Items.Count(x => x.Type == ProgramItemType.Meal), Items = p.Items.Where(x => x.Type == ProgramItemType.Meal).OrderBy(x => x.Order).Select(x => new NutritionPlanItemEditVm { Name = x.Title, Time = x.Time, Calories = x.Calories, ProductId = x.ProductId, Grams = decimal.TryParse(x.Dose, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var grams) ? grams : null }).ToList() };
    static UserFoodItemEditVm ToFoodVm(FoodProduct x) => new() { Name = x.Name, Kind = x.Kind, CategoryId = x.CategoryId, Image = x.Image, Calories = x.Calories, Protein = x.Protein, Fat = x.Fat, Carbs = x.Carbs, Description = x.Description, Instructions = x.Instructions, PreparationMinutes = x.PreparationMinutes };
}
