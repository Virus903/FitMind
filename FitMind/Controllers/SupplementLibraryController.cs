using FitMind.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Controllers;

[Authorize]
public class SupplementLibraryController(ApplicationDbContext db):Controller
{
    public async Task<IActionResult> Index(string? q,int? categoryId,string? sort,int page=1)
    {
        var a=db.Supplements.Include(x=>x.Category).Where(x=>x.Active&&x.OwnerId==null);
        if(!string.IsNullOrWhiteSpace(q))a=a.Where(x=>x.Name.Contains(q)||(x.ShortDescription??"").Contains(q)||(x.Purpose??"").Contains(q));
        if(categoryId.HasValue)a=a.Where(x=>x.CategoryId==categoryId);
        a=sort=="category"?a.OrderBy(x=>x.Category!.Name).ThenBy(x=>x.Name):a.OrderBy(x=>x.Name);
        page=Math.Max(1,page);const int pageSize=24;var total=await a.CountAsync();page=Math.Min(page,Math.Max(1,(int)Math.Ceiling(total/(double)pageSize)));
        ViewBag.Categories=await db.Categories.Where(x=>x.Section=="Добавки").OrderBy(x=>x.Name).ToListAsync();ViewBag.CategoryId=categoryId;ViewBag.Q=q;ViewBag.Pagination=new FitMind.ViewModels.PaginationVm(page,pageSize,total);
        return View(await a.AsNoTracking().Skip((page-1)*pageSize).Take(pageSize).ToListAsync());
    }
    public async Task<IActionResult> Details(int id){var x=await db.Supplements.Include(x=>x.Category).FirstOrDefaultAsync(x=>x.Id==id&&x.Active&&x.OwnerId==null);return x==null?NotFound():View(x);}
}
