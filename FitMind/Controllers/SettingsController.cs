using FitMind.Data;
using FitMind.Models;
using FitMind.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitMind.Controllers;

[Authorize]
public class SettingsController(ApplicationDbContext db,UserManager<ApplicationUser> users,SignInManager<ApplicationUser> signIn):Controller
{
    string Uid=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index()
    {
        // Opening settings must not mutate a user's data. The default object is
        // persisted only after the user explicitly submits the form.
        var x=await db.UserProfiles.FirstOrDefaultAsync(x=>x.UserId==Uid)
            ?? new UserProfile { UserId=Uid };
        return View(x);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(bool notificationsEnabled,bool emailNotifications,bool quietHoursEnabled,TimeSpan? quietStart,TimeSpan? quietEnd,string units,string theme)
    {
        if(!new[]{"Тёмная","Светлая","Системная"}.Contains(theme))ModelState.AddModelError(nameof(theme),"Выберите доступную тему.");
        if(!new[]{"Метрические","Имперские"}.Contains(units))ModelState.AddModelError(nameof(units),"Выберите доступные единицы.");
        if(quietHoursEnabled&&(!quietStart.HasValue||!quietEnd.HasValue||quietStart==quietEnd))ModelState.AddModelError("","Для тихих часов укажите различающиеся начало и окончание.");
        var x=await db.UserProfiles.FirstOrDefaultAsync(x=>x.UserId==Uid)??new UserProfile{UserId=Uid,Name=User.Identity?.Name?.Split('@')[0]??"Пользователь",Gender="Не указан",CreatedAt=DateTime.Now};
        if(!ModelState.IsValid)
        {
            // Keep the submitted state visible next to the validation message.
            x.NotificationsEnabled=notificationsEnabled;
            x.EmailNotifications=emailNotifications;
            x.QuietHoursEnabled=quietHoursEnabled;
            x.QuietStart=quietStart;
            x.QuietEnd=quietEnd;
            x.Units=units;
            x.Theme=theme;
            return View(x);
        }
        if(x.Id==0)db.UserProfiles.Add(x);
        x.NotificationsEnabled=notificationsEnabled;x.EmailNotifications=emailNotifications;x.QuietHoursEnabled=quietHoursEnabled;x.QuietStart=quietStart;x.QuietEnd=quietEnd;x.Units=units;x.Theme=theme;await db.SaveChangesAsync();TempData["Ok"]="Настройки сохранены.";return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Account()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        ViewBag.User = user;
        ViewBag.ExternalLogins = await users.GetLoginsAsync(user);
        ViewBag.HasPassword = await users.HasPasswordAsync(user);
        return View();
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword,string newPassword,string confirmPassword)
    {
        if(string.IsNullOrWhiteSpace(newPassword)||newPassword.Length<8){TempData["Error"]="Новый пароль должен содержать минимум 8 символов.";return RedirectToAction(nameof(Account));}
        if(newPassword!=confirmPassword){TempData["Error"]="Новые пароли не совпадают.";return RedirectToAction(nameof(Account));}
        var user=await users.GetUserAsync(User);if(user==null)return Challenge();
        var r=await users.HasPasswordAsync(user)
            ? await users.ChangePasswordAsync(user,currentPassword,newPassword)
            : await users.AddPasswordAsync(user,newPassword);
        TempData[r.Succeeded?"Ok":"Error"]=r.Succeeded?"Пароль изменён.":string.Join("; ",r.Errors.Select(x=>x.Description));return RedirectToAction(nameof(Account));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutEverywhere()
    {
        var user=await users.GetUserAsync(User); if(user==null)return Challenge();
        await users.UpdateSecurityStampAsync(user); await signIn.SignOutAsync();
        return RedirectToAction("Login","Account",new{message="all_sessions_closed"});
    }

    [HttpGet]
    public async Task<IActionResult> ExportData()
    {
        var user=await users.GetUserAsync(User); if(user==null)return Challenge();
        var data=new {
            Account=new{user.Id,user.Email,user.PhoneNumber},
            Profile=await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x=>x.UserId==Uid),
            Measurements=await db.BodyMeasurements.AsNoTracking().Where(x=>x.UserId==Uid).ToListAsync(),
            DayEntries=await db.DayEntries.AsNoTracking().Where(x=>x.UserId==Uid).ToListAsync(),
            Programs=await db.Programs.AsNoTracking().Where(x=>x.OwnerId==Uid).Include(x=>x.Items).ToListAsync(),
            Notes=await db.PersonalNotes.AsNoTracking().Where(x=>x.UserId==Uid).ToListAsync(),
            Notifications=await db.Notifications.AsNoTracking().Where(x=>x.UserId==Uid).ToListAsync(),
            Supplements=await db.Supplements.AsNoTracking().Where(x=>x.OwnerId==Uid).ToListAsync(),
            RecoveryMethods=await db.RecoveryMethods.AsNoTracking().Where(x=>x.OwnerId==Uid).ToListAsync()
        };
        return File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data,new System.Text.Json.JsonSerializerOptions{WriteIndented=true,ReferenceHandler=System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles}),"application/json",$"fitmind-data-{DateTime.Today:yyyy-MM-dd}.json");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(string password, string confirmation)
    {
        var user=await users.GetUserAsync(User); if(user==null)return Challenge();
        if(confirmation!="УДАЛИТЬ"){TempData["Error"]="Введите УДАЛИТЬ для подтверждения.";return RedirectToAction(nameof(Account));}
        if(await users.HasPasswordAsync(user)&&!await users.CheckPasswordAsync(user,password)){TempData["Error"]="Неверный пароль.";return RedirectToAction(nameof(Account));}
        await db.Notifications.Where(x=>x.UserId==Uid).ExecuteDeleteAsync();
        await db.PersonalNotes.Where(x=>x.UserId==Uid).ExecuteDeleteAsync();
        await db.BodyMeasurements.Where(x=>x.UserId==Uid).ExecuteDeleteAsync();
        await db.DayEntries.Where(x=>x.UserId==Uid).ExecuteDeleteAsync();
        await db.UserTrainingFormats.Where(x=>x.UserId==Uid).ExecuteDeleteAsync();
        await db.Programs.Where(x=>x.OwnerId==Uid).ExecuteDeleteAsync();
        await db.Supplements.Where(x=>x.OwnerId==Uid).ExecuteDeleteAsync();
        await db.RecoveryMethods.Where(x=>x.OwnerId==Uid).ExecuteDeleteAsync();
        await db.UserProfiles.Where(x=>x.UserId==Uid).ExecuteDeleteAsync();
        var result=await users.DeleteAsync(user); if(!result.Succeeded)throw new InvalidOperationException(string.Join("; ",result.Errors.Select(x=>x.Description)));
        await signIn.SignOutAsync();
        return RedirectToAction("Index","Home");
    }
}
