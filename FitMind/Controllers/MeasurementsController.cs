using FitMind.Data;
using FitMind.Models;
using FitMind.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitMind.Controllers;

[Authorize]
public class MeasurementsController(ApplicationDbContext db):Controller
{
    string Uid=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index()=>View(new MeasurementsPageVm{Types=await db.MeasurementTypes.OrderByDescending(x=>x.IsMain).ThenBy(x=>x.Name).ToListAsync(),History=await db.BodyMeasurements.Where(x=>x.UserId==Uid).Include(x=>x.Type).OrderByDescending(x=>x.DateTime).Take(100).ToListAsync()});

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int typeId,decimal value,DateTime date,string? comment)
    {
        if(!ModelState.IsValid||value<=0||value>10000||date.Year<2000||date.Year>2100||comment?.Length>2000||!await db.MeasurementTypes.AnyAsync(x=>x.Id==typeId)){TempData["Error"]="Выбери тип замера из базы и укажи положительное значение.";return RedirectToAction(nameof(Index));}
        db.BodyMeasurements.Add(new BodyMeasurement{UserId=Uid,TypeId=typeId,Value=value,DateTime=date.Date.Add(new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, 0)),Comment=comment});await db.SaveChangesAsync();TempData["Ok"]="Замер сохранён.";return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id){var x=await db.BodyMeasurements.FirstOrDefaultAsync(x=>x.Id==id&&x.UserId==Uid);if(x!=null){db.Remove(x);await db.SaveChangesAsync();}return RedirectToAction(nameof(Index));}
}
