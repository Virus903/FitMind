using FitMind.Data;
using FitMind.Models;
using FitMind.Services;
using FitMind.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class CatalogController(ApplicationDbContext db, AdminAuditService audit, IWebHostEnvironment environment) : Controller
{
    private static readonly Dictionary<string, string> Titles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["exercises"] = "Упражнения", ["products"] = "Продукты", ["recipes"] = "Рецепты", ["recovery"] = "Методы восстановления", ["supplements"] = "Добавки", ["articles"] = "Статьи"
    };
    private bool Valid(string kind) => kind is not null && Titles.ContainsKey(kind) && kind == kind.ToLowerInvariant();
    private string Title(string kind) => Titles[kind];

    public async Task<IActionResult> Index(string kind, string? q, string status = "all", string sort = "name", int page = 1)
    {
        if (!Valid(kind)) return NotFound();
        const int size = 15; page = Math.Max(1, page);
        status = status is "active" or "hidden" ? status : "all";
        var rows = await ReadRows(kind, q);
        if(status=="active") rows=rows.Where(x=>x.Active).ToList();
        if(status=="hidden") rows=rows.Where(x=>!x.Active).ToList();
        sort = sort is "name-desc" or "status" ? sort : "name";
        rows = sort switch { "name-desc"=>rows.OrderByDescending(x=>x.Name).ToList(), "status"=>rows.OrderByDescending(x=>x.Active).ThenBy(x=>x.Name).ToList(), _=>rows.OrderBy(x=>x.Name).ToList() };
        var totalPages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)size));
        page = Math.Min(page, totalPages);
        return View(new AdminListVm { Kind = kind, Title = Title(kind), Query = q, Status = status, Sort = sort, Page = page, TotalItems = rows.Count, TotalPages = totalPages, Rows = rows.Skip((page - 1) * size).Take(size).ToList() });
    }

    public async Task<IActionResult> Create(string kind) { if (!Valid(kind)) return NotFound(); await PopulateCategories(kind); return View("Edit", new AdminContentVm { Kind = kind, Active = true }); }
    public async Task<IActionResult> Edit(string kind, int id)
    {
        if (!Valid(kind)) return NotFound(); var vm = await ReadOne(kind, id); if (vm is null) return NotFound(); await PopulateCategories(kind);
        if(kind=="recipes") ViewBag.Ingredients=await db.RecipeIngredients.Include(x=>x.Product).AsNoTracking().Where(x=>x.RecipeId==id).OrderBy(x=>x.Order).ToListAsync();
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Save(AdminContentVm vm)
    {
        if (!Valid(vm.Kind)) return NotFound();
        var existing=vm.Id.HasValue?await ReadOne(vm.Kind,vm.Id.Value):null;
        if(vm.Id.HasValue && existing is null) return NotFound();
        vm.Image=existing?.Image;
        var validCategory = vm.Kind switch {
            "exercises" => await db.Categories.AnyAsync(x=>x.Section=="Упражнения"&&x.Id==vm.CategoryId),
            "products" => await db.Categories.AnyAsync(x=>x.Section=="Продукты"&&x.Id==vm.CategoryId),
            "recipes" => vm.CategoryId==null || await db.Categories.AnyAsync(x=>x.Section=="Продукты"&&x.Id==vm.CategoryId),
            "supplements" => await db.Categories.AnyAsync(x=>x.Section=="Добавки"&&x.Id==vm.CategoryId),
            "articles" => await db.Categories.AnyAsync(x=>x.Section=="Статьи"&&x.Id==vm.CategoryId), _ => true };
        if(!validCategory) ModelState.AddModelError("CategoryId","Выберите существующую категорию.");
        if(vm.Kind=="exercises") {
            if(await db.MuscleGroups.CountAsync(x=>vm.MuscleIds.Contains(x.Id))!=vm.MuscleIds.Distinct().Count()) ModelState.AddModelError("MuscleIds","Неизвестная группа мышц.");
            if(vm.PrimaryMuscleId.HasValue&&!vm.MuscleIds.Contains(vm.PrimaryMuscleId.Value)) ModelState.AddModelError("PrimaryMuscleId","Основная мышца должна быть отмечена в списке.");
        }
        if(vm.Kind=="products" && vm.Calories is null) ModelState.AddModelError("Calories","Укажите калорийность на 100 г.");
        var upload = await ContentImageStorage.SaveAsync(vm.ImageFile, environment);
        if(upload.Error is not null) ModelState.AddModelError(nameof(vm.ImageFile), upload.Error);
        if (!ModelState.IsValid) { await PopulateCategories(vm.Kind); if(vm.Kind=="recipes") ViewBag.Ingredients=await db.RecipeIngredients.Where(x=>x.RecipeId==vm.Id).OrderBy(x=>x.Order).ToListAsync(); return View("Edit", vm); }
        if(upload.Path is not null) vm.Image=upload.Path;
        else if(vm.RemoveImage) vm.Image=null;
        var result = await SaveOne(vm);
        await audit.WriteAsync(vm.Id.HasValue ? "Изменение" : "Создание", Title(vm.Kind), result, vm.Name);
        TempData["Ok"] = vm.Id.HasValue ? "Изменения сохранены." : "Запись создана.";
        return RedirectToAction(nameof(Edit), new { kind = vm.Kind, id = result });
    }

    public async Task<IActionResult> Delete(string kind, int id) { if (!Valid(kind)) return NotFound(); var vm = await ReadOne(kind, id); return vm is null ? NotFound() : View(vm); }
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(string kind, int id)
    {
        if (!Valid(kind)) return NotFound();
        if(await CatalogReferenceGuard.IsUsedAsync(db,kind,id))
        {
            TempData["Error"]="Запись используется в программах или личных данных. Вместо удаления снимите отметку публикации.";
            return RedirectToAction(nameof(Edit),new{kind,id});
        }
        try
        {
            var name = await DeleteOne(kind, id); if (name is null) return NotFound();
            await audit.WriteAsync("Удаление", Title(kind), id, name); TempData["Ok"] = "Запись удалена.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Запись используется связанными данными и не была удалена. Сначала измените зависимые записи.";
        }
        return RedirectToAction(nameof(Index), new { kind });
    }

    [HttpPost] public async Task<IActionResult> AddIngredient(int recipeId,int? productId,string? name,decimal? grams)
    {
        if(!ModelState.IsValid || grams is null or <=0 or >100000 || (productId.HasValue&&!await db.FoodProducts.AnyAsync(x=>x.Id==productId)) || name?.Length>180) {
            TempData["Error"]="Укажите существующий продукт и массу от 0,01 до 100000 г.";return RedirectToAction(nameof(Edit),new{kind="recipes",id=recipeId});
        }
        if(!await db.FoodProducts.AnyAsync(x=>x.Id==recipeId&&x.Kind==FoodProductKind.Recipe&&x.OwnerId==null))return NotFound();var product=productId.HasValue?await db.FoodProducts.FirstOrDefaultAsync(x=>x.Id==productId&&x.Kind==FoodProductKind.Product):null;var title=product?.Name??name?.Trim();if(string.IsNullOrWhiteSpace(title)){TempData["Error"]="Выберите продукт или укажите название.";return RedirectToAction(nameof(Edit),new{kind="recipes",id=recipeId});}var order=(short)((await db.RecipeIngredients.Where(x=>x.RecipeId==recipeId).MaxAsync(x=>(short?)x.Order)??0)+1);db.RecipeIngredients.Add(new RecipeIngredient{RecipeId=recipeId,ProductId=productId,Name=title,Grams=grams,Order=order});await db.SaveChangesAsync();await audit.WriteAsync("Добавление ингредиента","Рецепт",recipeId,title);return RedirectToAction(nameof(Edit),new{kind="recipes",id=recipeId});
    }
    [HttpPost] public async Task<IActionResult> DeleteIngredient(int recipeId,int ingredientId)
    {
        var x=await db.RecipeIngredients.FirstOrDefaultAsync(x=>x.Id==ingredientId&&x.RecipeId==recipeId);if(x is null)return NotFound();db.RecipeIngredients.Remove(x);await db.SaveChangesAsync();await audit.WriteAsync("Удаление ингредиента","Рецепт",recipeId,x.Name);return RedirectToAction(nameof(Edit),new{kind="recipes",id=recipeId});
    }

    [HttpPost] public async Task<IActionResult> EditIngredient(int recipeId,int ingredientId,string name,decimal grams,short order)
    {
        var item=await db.RecipeIngredients.FirstOrDefaultAsync(x=>x.Id==ingredientId&&x.RecipeId==recipeId);
        if(item is null)return NotFound();
        if(!ModelState.IsValid||string.IsNullOrWhiteSpace(name)||name.Length>180||grams<=0||grams>100000||order<1) TempData["Error"]="Проверьте название, массу и порядок ингредиента.";
        else {item.Name=name.Trim();item.Grams=grams;item.Order=order;await db.SaveChangesAsync();await audit.WriteAsync("Изменение ингредиента","Рецепт",recipeId,item.Name);TempData["Ok"]="Ингредиент сохранён.";}
        return RedirectToAction(nameof(Edit),new{kind="recipes",id=recipeId});
    }

    private async Task PopulateCategories(string kind)
    {
        ViewBag.Categories = kind switch
        {
            "exercises" => await db.Categories.AsNoTracking().Where(x=>x.Section=="Упражнения").OrderBy(x => x.Name).Select(x => new KeyValuePair<int, string>(x.Id, x.Name)).ToListAsync(),
            "products" or "recipes" => await db.Categories.AsNoTracking().Where(x=>x.Section=="Продукты").OrderBy(x => x.Name).Select(x => new KeyValuePair<int, string>(x.Id, x.Name)).ToListAsync(),
            "supplements" => await db.Categories.AsNoTracking().Where(x=>x.Section=="Добавки").OrderBy(x => x.Name).Select(x => new KeyValuePair<int, string>(x.Id, x.Name)).ToListAsync(),
            "articles" => await db.Categories.AsNoTracking().Where(x=>x.Section=="Статьи").OrderBy(x => x.Name).Select(x => new KeyValuePair<int, string>(x.Id, x.Name)).ToListAsync(),
            _ => []
        };
        if (kind == "exercises")
            ViewBag.Muscles = await db.MuscleGroups.AsNoTracking().OrderBy(x=>x.Name).Select(x=>new KeyValuePair<int,string>(x.Id,x.Name)).ToListAsync();
        if(kind=="recipes") ViewBag.Products=await db.FoodProducts.AsNoTracking().Where(x=>x.Kind==FoodProductKind.Product&&x.OwnerId==null).OrderBy(x=>x.Name).Select(x=>new KeyValuePair<int,string>(x.Id,x.Name)).ToListAsync();
    }

    private async Task<List<AdminListRowVm>> ReadRows(string kind, string? q)
    {
        bool Match(string name, string? text) => string.IsNullOrWhiteSpace(q) || name.Contains(q, StringComparison.OrdinalIgnoreCase) || (text?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
        return kind switch
        {
            "exercises" => (await db.Exercises.Include(x => x.Category).Where(x => x.OwnerId == null).AsNoTracking().ToListAsync()).Where(x => Match(x.Name, x.Description)).Select(x => new AdminListRowVm(x.Id, x.Name, x.Category?.Name, x.Active, x.Image, x.Description)).ToList(),
            "products" => (await db.FoodProducts.Include(x => x.Category).Where(x=>x.Kind==FoodProductKind.Product&&x.OwnerId==null).AsNoTracking().ToListAsync()).Where(x => Match(x.Name, x.Description)).Select(x => new AdminListRowVm(x.Id, x.Name, x.Category?.Name, x.Active, x.Image, x.Description)).ToList(),
            "recipes" => (await db.FoodProducts.Include(x => x.Category).Where(x=>x.Kind==FoodProductKind.Recipe&&x.OwnerId==null).AsNoTracking().ToListAsync()).Where(x => Match(x.Name, x.Description)).Select(x => new AdminListRowVm(x.Id, x.Name, x.Category?.Name, x.Active, x.Image, x.Description)).ToList(),
            "recovery" => (await db.RecoveryMethods.AsNoTracking().ToListAsync()).Where(x => Match(x.Name, x.ShortDescription)).Select(x => new AdminListRowVm(x.Id, x.Name, x.Category, x.Active, x.Image, x.ShortDescription)).ToList(),
            "supplements" => (await db.Supplements.Where(x => x.OwnerId == null).Include(x => x.Category).AsNoTracking().ToListAsync()).Where(x => Match(x.Name, x.ShortDescription)).Select(x => new AdminListRowVm(x.Id, x.Name, x.Category?.Name, x.Active, x.Image, x.ShortDescription)).ToList(),
            _ => (await db.Articles.Include(x => x.Category).AsNoTracking().ToListAsync()).Where(x => Match(x.Title, x.ShortDescription)).Select(x => new AdminListRowVm(x.Id, x.Title, x.Category?.Name, x.Published, x.Image, x.ShortDescription)).ToList()
        };
    }

    private async Task<AdminContentVm?> ReadOne(string kind, int id) => kind switch
    {
        "exercises" => await ReadExercise(id, kind),
        "products" => await db.FoodProducts.Where(x => x.Id == id&&x.Kind==FoodProductKind.Product&&x.OwnerId==null).Select(x => new AdminContentVm { Kind = kind, Id = x.Id, Name = x.Name, Description = x.Description, CategoryId = x.CategoryId, Image = x.Image, Active = x.Active, ServingGrams=x.ServingGrams, Calories=x.Calories, Protein=x.Protein, Fat=x.Fat, Carbs=x.Carbs, Fiber=x.Fiber, GlycemicIndex=x.GlycemicIndex, Details = x.Benefits, Tips=x.CookingTips }).FirstOrDefaultAsync(),
        "recipes" => await db.FoodProducts.Where(x => x.Id == id&&x.Kind==FoodProductKind.Recipe&&x.OwnerId==null).Select(x => new AdminContentVm { Kind = kind, Id = x.Id, Name = x.Name, Description = x.Description, CategoryId = x.CategoryId, Image = x.Image, Active = x.Active, PreparationMinutes=x.PreparationMinutes, ServingGrams=x.ServingGrams, Calories=x.Calories, Protein=x.Protein, Fat=x.Fat, Carbs=x.Carbs, Details = x.Instructions, Tips=x.CookingTips }).FirstOrDefaultAsync(),
        "recovery" => await db.RecoveryMethods.Where(x => x.Id == id).Select(x => new AdminContentVm { Kind = kind, Id = x.Id, Name = x.Name, Description = x.ShortDescription, Image = x.Image, Active = x.Active, Meta = x.Category, Details = x.FullDescription }).FirstOrDefaultAsync(),
        "supplements" => await db.Supplements.Where(x => x.Id == id && x.OwnerId == null).Select(x => new AdminContentVm { Kind = kind, Id = x.Id, Name = x.Name, Description = x.ShortDescription, CategoryId = x.CategoryId, Image = x.Image, Active = x.Active, Meta = x.Form, Details = x.Usage }).FirstOrDefaultAsync(),
        "articles" => await db.Articles.Where(x => x.Id == id).Select(x => new AdminContentVm { Kind = kind, Id = x.Id, Name = x.Title, Description = x.ShortDescription, CategoryId = x.CategoryId, Image = x.Image, Active = x.Published, Meta = x.ReadingMinutes.ToString(), Details = x.Text }).FirstOrDefaultAsync(),
        _ => null
    };

    private async Task<int> SaveOne(AdminContentVm vm)
    {
        int category = vm.CategoryId ?? 0;
        switch (vm.Kind)
        {
            case "exercises": { var x = vm.Id is int id ? await db.Exercises.FindAsync(id) : new Exercise { CategoryId = category }; if (x is null) throw new InvalidOperationException(); x.CategoryId = category; x.Name = vm.Name; x.Description = vm.Description; x.Image = vm.Image; x.Active = vm.Active; x.Difficulty = string.IsNullOrWhiteSpace(vm.Meta) ? "Начальный" : vm.Meta; x.Technique = vm.Details; if (vm.Id is null) db.Exercises.Add(x); await db.SaveChangesAsync(); var old=await db.ExerciseMuscles.Where(m=>m.ExerciseId==x.Id).ToListAsync();db.ExerciseMuscles.RemoveRange(old);foreach(var muscleId in vm.MuscleIds.Distinct())db.ExerciseMuscles.Add(new ExerciseMuscle{ExerciseId=x.Id,MuscleGroupId=muscleId,IsPrimary=muscleId==vm.PrimaryMuscleId});await db.SaveChangesAsync(); return x.Id; }
            case "products": { var x = vm.Id is int id ? await db.FoodProducts.FirstOrDefaultAsync(x=>x.Id==id&&x.Kind==FoodProductKind.Product&&x.OwnerId==null) : new FoodProduct { CategoryId = category, Kind=FoodProductKind.Product }; if (x is null) throw new InvalidOperationException(); x.CategoryId = category; x.Name = vm.Name; x.Description = vm.Description; x.Image = vm.Image; x.Active = vm.Active; x.ServingGrams=vm.ServingGrams; x.Calories=vm.Calories??0; x.Protein=vm.Protein??0; x.Fat=vm.Fat??0; x.Carbs=vm.Carbs??0; x.Fiber=vm.Fiber; x.GlycemicIndex=vm.GlycemicIndex; x.Benefits=vm.Details; x.CookingTips=vm.Tips; if (vm.Id is null) db.FoodProducts.Add(x); await db.SaveChangesAsync(); return x.Id; }
            case "recipes": { var x = vm.Id is int id ? await db.FoodProducts.FirstOrDefaultAsync(x=>x.Id==id&&x.Kind==FoodProductKind.Recipe&&x.OwnerId==null) : new FoodProduct { CategoryId = category, Kind=FoodProductKind.Recipe }; if (x is null) throw new InvalidOperationException(); x.CategoryId = category; x.Name = vm.Name; x.Description = vm.Description; x.Image = vm.Image; x.Active = vm.Active; x.PreparationMinutes=vm.PreparationMinutes; x.ServingGrams=vm.ServingGrams; x.Calories=vm.Calories??0; x.Protein=vm.Protein??0; x.Fat=vm.Fat??0; x.Carbs=vm.Carbs??0; x.Instructions = vm.Details; x.CookingTips=vm.Tips; if (vm.Id is null) db.FoodProducts.Add(x); await db.SaveChangesAsync(); return x.Id; }
            case "recovery": { var x = vm.Id is int id ? await db.RecoveryMethods.FindAsync(id) : new RecoveryMethod(); if (x is null) throw new InvalidOperationException(); x.Name = vm.Name; x.ShortDescription = vm.Description; x.Image = vm.Image; x.Active = vm.Active; x.Category = string.IsNullOrWhiteSpace(vm.Meta) ? "Восстановление" : vm.Meta; x.FullDescription = vm.Details; if (vm.Id is null) db.RecoveryMethods.Add(x); await db.SaveChangesAsync(); return x.Id; }
            case "supplements": { var x = vm.Id is int id ? await db.Supplements.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == null) : new Supplement { CategoryId = category }; if (x is null) throw new InvalidOperationException(); x.CategoryId = category; x.Name = vm.Name; x.ShortDescription = vm.Description; x.Image = vm.Image; x.Active = vm.Active; x.Form = vm.Meta; x.Usage = vm.Details; if (vm.Id is null) db.Supplements.Add(x); await db.SaveChangesAsync(); return x.Id; }
            default: { var x = vm.Id is int id ? await db.Articles.FindAsync(id) : new Article { CategoryId = category, PublishedAt = DateTime.UtcNow }; if (x is null) throw new InvalidOperationException(); x.CategoryId = category; x.Title = vm.Name; x.ShortDescription = vm.Description; x.Image = vm.Image; x.Published = vm.Active; x.ReadingMinutes = short.TryParse(vm.Meta, out var mins) ? mins : null; x.Text = vm.Details ?? ""; if (vm.Id is null) db.Articles.Add(x); await db.SaveChangesAsync(); return x.Id; }
        }
    }

    private async Task<AdminContentVm?> ReadExercise(int id,string kind)
    {
        var x=await db.Exercises.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);if(x is null)return null;
        var muscles=await db.ExerciseMuscles.AsNoTracking().Where(m=>m.ExerciseId==id).ToListAsync();
        return new AdminContentVm{Kind=kind,Id=x.Id,Name=x.Name,Description=x.Description,CategoryId=x.CategoryId,Image=x.Image,Active=x.Active,Meta=x.Difficulty,Details=x.Technique,MuscleIds=muscles.Select(m=>m.MuscleGroupId).ToList(),PrimaryMuscleId=muscles.FirstOrDefault(m=>m.IsPrimary)?.MuscleGroupId};
    }

    private async Task<string?> DeleteOne(string kind, int id)
    {
        switch (kind)
        {
            case "exercises": { var x = await db.Exercises.FindAsync(id); if (x is null) return null; db.Exercises.Remove(x); await db.SaveChangesAsync(); return x.Name; }
            case "products": { var x = await db.FoodProducts.FirstOrDefaultAsync(x=>x.Id==id&&x.Kind==FoodProductKind.Product&&x.OwnerId==null); if (x is null) return null; db.FoodProducts.Remove(x); await db.SaveChangesAsync(); return x.Name; }
            case "recipes": { var x = await db.FoodProducts.Include(x => x.Ingredients).FirstOrDefaultAsync(x => x.Id == id&&x.Kind==FoodProductKind.Recipe&&x.OwnerId==null); if (x is null) return null; db.RecipeIngredients.RemoveRange(x.Ingredients); db.FoodProducts.Remove(x); await db.SaveChangesAsync(); return x.Name; }
            case "recovery": { var x = await db.RecoveryMethods.FindAsync(id); if (x is null) return null; db.RecoveryMethods.Remove(x); await db.SaveChangesAsync(); return x.Name; }
            case "supplements": { var x = await db.Supplements.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == null); if (x is null) return null; db.Supplements.Remove(x); await db.SaveChangesAsync(); return x.Name; }
            default: { var x = await db.Articles.FindAsync(id); if (x is null) return null; db.Articles.Remove(x); await db.SaveChangesAsync(); return x.Title; }
        }
    }
}
