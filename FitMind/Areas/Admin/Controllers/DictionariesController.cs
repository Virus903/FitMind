using FitMind.Data;
using FitMind.Models;
using FitMind.Services;
using FitMind.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Policy = "AdminOnly")]
public sealed class DictionariesController(ApplicationDbContext db, AdminAuditService audit) : Controller
{
    private static readonly Dictionary<string,string> Titles = new() { ["exercise-categories"]="Категории упражнений", ["muscles"]="Группы мышц", ["food-categories"]="Категории продуктов", ["supplement-categories"]="Категории добавок", ["article-categories"]="Категории статей" };
    public async Task<IActionResult> Index(string kind)
    {
        if(!Titles.ContainsKey(kind)) return NotFound(); ViewBag.Kind=kind; ViewBag.Title=Titles[kind];
        ViewBag.Rows=kind=="muscles"?await db.MuscleGroups.AsNoTracking().OrderBy(x=>x.Name).Select(x=>new AdminDictionaryVm{Kind=kind,Id=x.Id,Name=x.Name,Description=x.Description}).ToListAsync():await db.Categories.AsNoTracking().Where(x=>x.Section==Section(kind)).OrderBy(x=>x.Name).Select(x=>new AdminDictionaryVm{Kind=kind,Id=x.Id,Name=x.Name,Description=x.Description}).ToListAsync(); return View();
    }
    [HttpPost] public async Task<IActionResult> Save(AdminDictionaryVm v)
    {
        if(!Titles.ContainsKey(v.Kind))return NotFound();
        if(v.Id.HasValue && await Find(v.Kind,v.Id.Value) is null)return NotFound();
        if(!ModelState.IsValid){TempData["Error"]="Название обязательно (до 120 символов), описание — до 800 символов.";return RedirectToAction(nameof(Index),new{kind=v.Kind});}
        v.Name=v.Name.Trim();
        var id=await SaveOne(v);
        await audit.WriteAsync(v.Id.HasValue?"Изменение":"Создание",Titles[v.Kind],id,v.Name);
        TempData["Ok"]="Справочник сохранён.";return RedirectToAction(nameof(Index),new{kind=v.Kind});
    }
    [HttpPost] public async Task<IActionResult> Delete(string kind,int id)
    {
        if(kind is null || !Titles.ContainsKey(kind))return NotFound();
        if(await Find(kind,id) is null)return NotFound();
        if(await CatalogReferenceGuard.IsUsedAsync(db,kind,id))
        {TempData["Error"]="Запись используется в каталоге. Сначала измените связанные записи.";return RedirectToAction(nameof(Index),new{kind});}
        var name=await DeleteOne(kind,id);
        await audit.WriteAsync("Удаление",Titles[kind],id,name ?? "");
        TempData["Ok"]="Запись удалена.";return RedirectToAction(nameof(Index),new{kind});
    }
    private async Task<object?> Find(string kind,int id) => kind switch
    {
        "muscles"=>await db.MuscleGroups.FindAsync(id),
        _=>await db.Categories.FirstOrDefaultAsync(x=>x.Id==id&&x.Section==Section(kind))
    };
    private async Task<int> SaveOne(AdminDictionaryVm v){if(v.Kind=="muscles"){var x=v.Id is int i?await db.MuscleGroups.FindAsync(i):new MuscleGroup();x!.Name=v.Name;x.Description=v.Description;if(v.Id is null)db.MuscleGroups.Add(x);await db.SaveChangesAsync();return x.Id;}var category=v.Id is int id?await db.Categories.FirstAsync(x=>x.Id==id&&x.Section==Section(v.Kind)):new Category{Section=Section(v.Kind)};category.Name=v.Name;category.Description=v.Description;if(v.Id is null)db.Categories.Add(category);await db.SaveChangesAsync();return category.Id;}
    private async Task<string?> DeleteOne(string kind,int id){if(kind=="muscles"){var muscle=await db.MuscleGroups.FindAsync(id);if(muscle is null)return null;db.Remove(muscle);await db.SaveChangesAsync();return muscle.Name;}var category=await db.Categories.FirstOrDefaultAsync(x=>x.Id==id&&x.Section==Section(kind));if(category is null)return null;db.Remove(category);await db.SaveChangesAsync();return category.Name;}
    private static string Section(string kind)=>kind switch{"exercise-categories"=>"Упражнения","food-categories"=>"Продукты","supplement-categories"=>"Добавки",_=>"Статьи"};
}
