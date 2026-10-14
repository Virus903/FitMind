using FitMind.Data;
using FitMind.Models.Identity;
using FitMind.Services;
using FitMind.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users, AdminAuditService audit) : Controller
{
    public async Task<IActionResult> Index()
    {
        var actions = await db.AdminAuditLogs.AsNoTracking().OrderByDescending(x => x.OccurredAt).Take(10).ToListAsync();
        var actorIds = actions.Where(x => x.ActorUserId != null).Select(x => x.ActorUserId!).Distinct().ToList();
        var names = await users.Users.Where(x => actorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Email ?? x.UserName ?? "—");
        return View(new AdminDashboardVm
        {
            Users = await users.Users.CountAsync(), Exercises = await db.Exercises.CountAsync(x => x.OwnerId == null), Programs = await db.Programs.CountAsync(x => x.OwnerId == null),
            Products = await db.FoodProducts.CountAsync(x => x.Kind == FitMind.Models.FoodProductKind.Product && x.OwnerId == null), Recipes = await db.FoodProducts.CountAsync(x => x.Kind == FitMind.Models.FoodProductKind.Recipe && x.OwnerId == null), Supplements = await db.Supplements.CountAsync(x => x.OwnerId == null), Articles = await db.Articles.CountAsync(),
            RecentActions = actions.Select(x => new AdminAuditRowVm(x.OccurredAt, x.Action, x.EntityType, x.EntityId, x.Summary, x.ActorUserId != null && names.TryGetValue(x.ActorUserId, out var n) ? n : "Система")).ToList()
        });
    }

    public async Task<IActionResult> Users(string? q, int page = 1)
    {
        const int size = 20;
        var query = users.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => (x.Email ?? "").Contains(q) || (x.UserName ?? "").Contains(q));
        var total = await query.CountAsync();
        var currentPage = Math.Max(page, 1);
        ViewBag.Query = q; ViewBag.Page = currentPage; ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        return View(await query.OrderBy(x => x.Email).Skip((currentPage - 1) * size).Take(size).ToListAsync());
    }

    public async Task<IActionResult> UserDetails(string id)
    {
        var user = await users.FindByIdAsync(id); if (user is null) return NotFound();
        ViewBag.Roles = await users.GetRolesAsync(user);
        return View("User", user);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLock(string id)
    {
        var user = await users.FindByIdAsync(id); if (user is null) return NotFound();
        if (user.Id == users.GetUserId(User)) { TempData["Error"] = "Нельзя заблокировать собственный аккаунт."; return RedirectToAction(nameof(UserDetails), new { id }); }
        var locked = user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow;
        user.LockoutEnabled = true; user.LockoutEnd = locked ? DateTimeOffset.UtcNow.AddYears(10) : null;
        var result = await users.UpdateAsync(user);
        if (result.Succeeded) await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) { TempData["Error"] = string.Join(" ", result.Errors.Select(x => x.Description)); return RedirectToAction(nameof(UserDetails), new { id }); }
        await audit.WriteAsync(locked ? "Блокировка" : "Разблокировка", "Пользователь", user.Id, user.Email ?? user.UserName ?? user.Id);
        TempData["Ok"] = locked ? "Пользователь заблокирован." : "Пользователь разблокирован.";
        return RedirectToAction(nameof(UserDetails), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleAdminRole(string id)
    {
        var user = await users.FindByIdAsync(id); if (user is null) return NotFound();
        if (user.Id == users.GetUserId(User)) { TempData["Error"]="Нельзя снять собственную роль администратора."; return RedirectToAction(nameof(UserDetails),new{id}); }
        var isAdmin=await users.IsInRoleAsync(user,"Администратор");
        var result=isAdmin?await users.RemoveFromRoleAsync(user,"Администратор"):await users.AddToRoleAsync(user,"Администратор");
        if(result.Succeeded) await users.UpdateSecurityStampAsync(user);
        if(!result.Succeeded){TempData["Error"]=string.Join(" ",result.Errors.Select(x=>x.Description));return RedirectToAction(nameof(UserDetails),new{id});}
        await audit.WriteAsync(isAdmin?"Снятие роли":"Назначение роли","Пользователь",id,user.Email??id);TempData["Ok"]=isAdmin?"Роль администратора снята.":"Роль администратора назначена.";return RedirectToAction(nameof(UserDetails),new{id});
    }
}
