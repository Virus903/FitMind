using FitMind.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace FitMind.Controllers;
[Authorize]
public class NotificationsController(ApplicationDbContext db):Controller
{
 string Uid=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 public async Task<IActionResult> Index() {
  var pref=await db.UserProfiles.FirstOrDefaultAsync(x=>x.UserId==Uid);
  var now=DateTime.Now;var quiet=false;
  if(pref?.QuietHoursEnabled==true&&pref.QuietStart.HasValue&&pref.QuietEnd.HasValue)quiet=pref.QuietStart<=pref.QuietEnd?now.TimeOfDay>=pref.QuietStart&&now.TimeOfDay<pref.QuietEnd:now.TimeOfDay>=pref.QuietStart||now.TimeOfDay<pref.QuietEnd;
  if(pref?.NotificationsEnabled!=false&&!quiet){
   await FitMind.Services.ScheduleSyncService.SyncAsync(db,Uid,DateTime.Today);
   var due=await db.DayEntries.Where(x=>x.UserId==Uid&&x.Start>=DateTime.Today&&x.Start<=now&&x.Status==FitMind.Models.DayEntryStatus.Planned).ToListAsync();
   foreach(var x in due){var link=$"/Schedule/Edit/{x.Id}";if(!await db.Notifications.AnyAsync(n=>n.UserId==Uid&&n.Link==link)){db.Notifications.Add(new FitMind.Models.Notification{UserId=Uid,Type="Режим дня",Title=x.Title,Text=$"Запланировано на {x.Start:HH:mm}. Отметьте результат в режиме дня.",Link=link,CreatedAt=now});}}
   await db.SaveChangesAsync();
  }
  return View(await db.Notifications.Where(x=>x.UserId==Uid).OrderByDescending(x=>x.CreatedAt).ToListAsync());
 }
 [HttpPost] public async Task<IActionResult> MarkRead(long id){var n=await db.Notifications.FirstOrDefaultAsync(x=>x.Id==id&&x.UserId==Uid);if(n!=null){n.Read=true;await db.SaveChangesAsync();}return RedirectToAction(nameof(Index));}
}
