using FitMind.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Controllers;

[Authorize]
public class ExercisesController(ApplicationDbContext db) : Controller
{
    string Uid => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
    public async Task<IActionResult> Index(string? q, string? muscle, string? place, string? difficulty, string? sort, int page=1)
    {
        var a=db.Exercises.Include(x=>x.Category).Where(x=>x.Active&&x.OwnerId==null);
        if(!string.IsNullOrWhiteSpace(q)) a=a.Where(x=>x.Name.Contains(q)||(x.Description??"").Contains(q));
        if(!string.IsNullOrWhiteSpace(place)) a=a.Where(x=>x.Place==place);
        if(!string.IsNullOrWhiteSpace(difficulty)) a=a.Where(x=>x.Difficulty==difficulty);
        if(!string.IsNullOrWhiteSpace(muscle))
        {
            var ids=await db.ExerciseMuscles.Where(x=>x.MuscleGroup!.Name==muscle).Select(x=>x.ExerciseId).ToListAsync();
            a=a.Where(x=>ids.Contains(x.Id));
        }
        a=sort=="difficulty"?a.OrderBy(x=>x.Difficulty):a.OrderBy(x=>x.Name);
        page=Math.Max(1,page);const int pageSize=24;var total=await a.CountAsync();page=Math.Min(page,Math.Max(1,(int)Math.Ceiling(total/(double)pageSize)));
        ViewBag.Muscles=await db.MuscleGroups.OrderBy(x=>x.Name).ToListAsync(); ViewBag.Q=q; ViewBag.Muscle=muscle; ViewBag.Place=place; ViewBag.Difficulty=difficulty;ViewBag.Pagination=new FitMind.ViewModels.PaginationVm(page,pageSize,total);
        return View(await a.AsNoTracking().Skip((page-1)*pageSize).Take(pageSize).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var x=await db.Exercises.Include(x=>x.Category).FirstOrDefaultAsync(x=>x.Id==id && x.Active && x.OwnerId==null); if(x==null)return NotFound();
        ViewBag.Muscles=await db.ExerciseMuscles.Where(m=>m.ExerciseId==id).Include(m=>m.MuscleGroup).OrderByDescending(x=>x.IsPrimary).ToListAsync();
        return View(x);
    }

    public IActionResult AddToWorkout(int id)=>RedirectToAction("Create","MyWorkouts",new{exerciseId=id});

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToMy(int id)
    {
        if(await MyWorkoutsController.CopyBaseExerciseAsync(db,Uid,id)==null)return NotFound();
        TempData["Ok"]="Упражнение добавлено в «Мои упражнения».";
        return RedirectToAction("Index","MyWorkouts");
    }
}
