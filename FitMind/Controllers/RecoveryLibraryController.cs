using FitMind.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Controllers;

[Authorize]
public class RecoveryLibraryController(ApplicationDbContext db):Controller
{
    public async Task<IActionResult> Index(string? q,string? category,string? sort,int page=1)
    {
        var a=db.RecoveryMethods.Where(x=>x.Active&&x.OwnerId==null);
        if(!string.IsNullOrWhiteSpace(q))a=a.Where(x=>x.Name.Contains(q)||(x.ShortDescription??"").Contains(q));
        if(!string.IsNullOrWhiteSpace(category))a=a.Where(x=>x.Category==category);
        a=sort=="duration"?a.OrderBy(x=>x.MinMinutes):a.OrderBy(x=>x.Name);
        page=Math.Max(1,page);const int pageSize=24;var total=await a.CountAsync();page=Math.Min(page,Math.Max(1,(int)Math.Ceiling(total/(double)pageSize)));
        ViewBag.Categories=await db.RecoveryMethods.Where(x=>x.Active&&x.OwnerId==null).Select(x=>x.Category).Distinct().OrderBy(x=>x).ToListAsync();ViewBag.Q=q;ViewBag.Category=category;ViewBag.Pagination=new FitMind.ViewModels.PaginationVm(page,pageSize,total);
        return View(await a.AsNoTracking().Skip((page-1)*pageSize).Take(pageSize).ToListAsync());
    }
    public async Task<IActionResult> Details(int id){var x=await db.RecoveryMethods.FirstOrDefaultAsync(x=>x.Id==id&&x.Active&&x.OwnerId==null);return x==null?NotFound():View(x);}
}
