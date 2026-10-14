using FitMind.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Controllers;

[Authorize]
public class KnowledgeController(ApplicationDbContext db):Controller
{
    public async Task<IActionResult> Index(string? q,int? categoryId,string? sort,int page=1)
    {
        var a=db.Articles.Include(x=>x.Category).Where(x=>x.Published);
        if(!string.IsNullOrWhiteSpace(q))a=a.Where(x=>x.Title.Contains(q)||(x.ShortDescription??"").Contains(q)||(x.Text??"").Contains(q));
        if(categoryId.HasValue)a=a.Where(x=>x.CategoryId==categoryId);
        a=sort=="short"?a.OrderBy(x=>x.ReadingMinutes):a.OrderByDescending(x=>x.PublishedAt);
        page=Math.Max(1,page);const int pageSize=18;var total=await a.CountAsync();page=Math.Min(page,Math.Max(1,(int)Math.Ceiling(total/(double)pageSize)));
        ViewBag.Categories=await db.Categories.Where(x=>x.Section=="Статьи").OrderBy(x=>x.Name).ToListAsync();ViewBag.Q=q;ViewBag.CategoryId=categoryId;ViewBag.Pagination=new FitMind.ViewModels.PaginationVm(page,pageSize,total);
        return View(await a.AsNoTracking().Skip((page-1)*pageSize).Take(pageSize).ToListAsync());
    }
    public async Task<IActionResult> Article(int id){var x=await db.Articles.Include(x=>x.Category).FirstOrDefaultAsync(x=>x.Id==id&&x.Published);return x==null?NotFound():View(x);}
}
