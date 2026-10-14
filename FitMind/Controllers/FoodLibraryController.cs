using FitMind.Data;
using FitMind.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Controllers;

[Authorize]
public class FoodLibraryController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? q, string? type, string? category, string? sort,int page=1)
    {
        var query=db.FoodProducts.Include(x=>x.Category).Where(x=>x.Active&&x.OwnerId==null);
        if(!string.IsNullOrWhiteSpace(q))query=query.Where(x=>x.Name.Contains(q)||(x.Description??"").Contains(q));
        if(!string.IsNullOrWhiteSpace(category))query=query.Where(x=>x.Category!.Name==category);
        if(type=="products")query=query.Where(x=>x.Kind==Models.FoodProductKind.Product);if(type=="recipes")query=query.Where(x=>x.Kind==Models.FoodProductKind.Recipe);
        query=sort=="calories"?query.OrderBy(x=>x.Calories):query.OrderBy(x=>x.Name);
        page=Math.Max(1,page);const int pageSize=12;var total=await query.CountAsync();page=Math.Min(page,Math.Max(1,(int)Math.Ceiling(total/(double)pageSize)));
        var rows=await query.AsNoTracking().Skip((page-1)*pageSize).Take(pageSize).ToListAsync();var products=rows.Where(x=>x.Kind==Models.FoodProductKind.Product).ToList();var recipes=rows.Where(x=>x.Kind==Models.FoodProductKind.Recipe).ToList();
        ViewBag.Categories=await db.Categories.Where(x=>x.Section=="Продукты").OrderBy(x=>x.Name).ToListAsync();
        ViewBag.Pagination=new PaginationVm(page,pageSize,total);
        return View(new FoodLibraryVm{Products=products,Recipes=recipes,Query=q,Type=type,Category=category,Sort=sort});
    }

    public async Task<IActionResult> Product(int id){var x=await db.FoodProducts.Include(x=>x.Category).FirstOrDefaultAsync(x=>x.Id==id&&x.Active&&x.OwnerId==null&&x.Kind==Models.FoodProductKind.Product);return x==null?NotFound():View(x);}
    public async Task<IActionResult> Recipe(int id){var x=await db.FoodProducts.Include(x=>x.Category).Include(x=>x.Ingredients).ThenInclude(x=>x.Product).FirstOrDefaultAsync(x=>x.Id==id&&x.Active&&x.OwnerId==null&&x.Kind==Models.FoodProductKind.Recipe);return x==null?NotFound():View(x);}
    public IActionResult AddProductToDiary(int id)=>RedirectToAction("AddMeal","FoodDiary",new{productId=id});
    public IActionResult AddRecipeToDiary(int id)=>RedirectToAction("AddMeal","FoodDiary",new{productId=id});
}
