using FitMind.Data;
using FitMind.Models;
using FitMind.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitMind.Controllers;

[Authorize]
public class ProfileController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
{
    string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    string AvatarPath => Path.Combine(environment.ContentRootPath, "App_Data", "avatars", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Uid))) + ".img");
    [HttpGet]
    public IActionResult Avatar()
    {
        Response.Headers.CacheControl = "no-store";
        if (!System.IO.File.Exists(AvatarPath)) return PhysicalFile(Path.Combine(environment.WebRootPath,"images","avatar-default.svg"),"image/svg+xml");
        var data = System.IO.File.ReadAllBytes(AvatarPath);
        return File(data, data[0]==0x89 ? "image/png" : data[0]==0xff ? "image/jpeg" : "image/webp");
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAvatar(IFormFile? avatar)
    {
        if(avatar==null || avatar.Length<12 || avatar.Length>2*1024*1024) {TempData["Error"]="Выберите PNG, JPEG или WebP размером до 2 МБ.";return RedirectToAction(nameof(Index));}
        using var stream=new MemoryStream();await avatar.CopyToAsync(stream);var data=stream.ToArray();
        bool png=data.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
        bool jpeg=data[0]==255&&data[1]==216&&data[2]==255;
        bool webp=System.Text.Encoding.ASCII.GetString(data,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(data,8,4)=="WEBP";
        if(!(png||jpeg||webp)){TempData["Error"]="Файл не является PNG, JPEG или WebP.";return RedirectToAction(nameof(Index));}
        Directory.CreateDirectory(Path.GetDirectoryName(AvatarPath)!);
        var temp=AvatarPath+Guid.NewGuid().ToString("N");await System.IO.File.WriteAllBytesAsync(temp,data);System.IO.File.Move(temp,AvatarPath,true);
        TempData["Ok"]="Аватар обновлён.";return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult DeleteAvatar(){if(System.IO.File.Exists(AvatarPath))System.IO.File.Delete(AvatarPath);TempData["Ok"]="Аватар удалён.";return RedirectToAction(nameof(Index));}

    public async Task<IActionResult> Index()
    {
        var profile = await db.UserProfiles
            .Include(x => x.TrainingLevel)
            .Include(x => x.ActivityLevel)
            .Include(x => x.Goal)
            .FirstOrDefaultAsync(x => x.UserId == Uid);

        if (profile == null || !profile.OnboardingCompleted)
            return RedirectToCurrentStep(profile);

        var goalName = profile.Goal?.Name;

        var formats = await db.UserTrainingFormats
            .Where(x => x.UserId == Uid)
            .Include(x => x.Format)
            .Select(x => x.Format!)
            .OrderBy(x => x.Name)
            .ToListAsync();
        return View(new ProfilePageVm
        {
            Profile = profile,
            GoalName = goalName,
            Formats = formats,
            ActivePrograms = await db.Programs.AsNoTracking().Where(x => x.OwnerId == Uid && x.Active).OrderBy(x => x.Kind).ToListAsync()
        });
    }

    // Старый адрес оставлен, чтобы ссылки из предыдущей версии проекта не ломались.
    [HttpGet]
    public IActionResult Questionnaire() => RedirectToAction(nameof(Setup));

    [HttpGet]
    public async Task<IActionResult> Setup()
    {
        await FillActivities();
        var profile = await GetOrCreateProfile();
        return View(new ProfileSetupVm
        {
            Age = profile.Age,
            HeightCm = profile.HeightCm,
            WeightKg = profile.WeightKg,
            Gender = profile.Gender ?? "Не указан",
            ActivityLevelId = profile.ActivityLevelId,
            AboutMe = profile.AboutMe
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup(ProfileSetupVm vm)
    {
        await FillActivities();
        await ValidateReferences(activity:vm.ActivityLevelId);
        if (!ModelState.IsValid) return View(vm);

        var profile = await GetOrCreateProfile();
        profile.Age = vm.Age;
        profile.HeightCm = vm.HeightCm;
        profile.WeightKg = vm.WeightKg;
        profile.Gender = vm.Gender;
        profile.ActivityLevelId = vm.ActivityLevelId;
        profile.AboutMe = string.IsNullOrWhiteSpace(vm.AboutMe) ? null : vm.AboutMe.Trim();
        profile.OnboardingStep = 2;
        profile.OnboardingCompleted = false;
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Goal));
    }

    [HttpGet]
    public async Task<IActionResult> Goal()
    {
        await FillGoalsAndLevels();
        var profile = await GetOrCreateProfile();
        return View(new GoalStepVm
        {
            GoalId = profile.GoalId,
            TrainingLevelId = profile.TrainingLevelId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Goal(GoalStepVm vm)
    {
        await FillGoalsAndLevels();
        await ValidateReferences(goal:vm.GoalId,level:vm.TrainingLevelId);
        if (!ModelState.IsValid) return View(vm);

        var profile = await GetOrCreateProfile();
        profile.TrainingLevelId = vm.TrainingLevelId;
        profile.GoalId = vm.GoalId;
        profile.OnboardingStep = 3;
        profile.OnboardingCompleted = false;

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(TrainingPreferences));
    }

    [HttpGet]
    public async Task<IActionResult> TrainingPreferences()
    {
        await FillTrainingPreferences();
        var profile = await GetOrCreateProfile();
        var formats = await db.UserTrainingFormats
            .Where(x => x.UserId == Uid)
            .Select(x => x.FormatId)
            .ToListAsync();
        return View(new TrainingPreferencesVm
        {
            AtHome = profile.AtHome,
            Outdoor = profile.Outdoor,
            AtGym = profile.AtGym,
            WorkoutsPerWeek = profile.WorkoutsPerWeek,
            FormatIds = formats
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TrainingPreferences(TrainingPreferencesVm vm)
    {
        await FillTrainingPreferences();
        vm.FormatIds ??= new List<int>();
        await ValidateReferences(formats:vm.FormatIds);
        if (!vm.AtHome && !vm.Outdoor && !vm.AtGym)
            ModelState.AddModelError(nameof(vm.AtHome), "Выберите хотя бы одно место тренировок.");
        if (vm.FormatIds.Count == 0)
            ModelState.AddModelError(nameof(vm.FormatIds), "Выберите хотя бы один формат тренировок.");
        if (!ModelState.IsValid) return View(vm);

        var profile = await GetOrCreateProfile();
        profile.AtHome = vm.AtHome;
        profile.Outdoor = vm.Outdoor;
        profile.AtGym = vm.AtGym;
        profile.WorkoutsPerWeek = vm.WorkoutsPerWeek;
        profile.OnboardingStep = 4;
        profile.OnboardingCompleted = true;

        var oldFormats = await db.UserTrainingFormats.Where(x => x.UserId == Uid).ToListAsync();
        db.UserTrainingFormats.RemoveRange(oldFormats);
        foreach (var formatId in vm.FormatIds.Distinct())
        {
            db.UserTrainingFormats.Add(new UserTrainingFormat
            {
                UserId = Uid,
                FormatId = formatId,
                CreatedAt = DateTime.Now
            });
        }

        await db.SaveChangesAsync();
        TempData["Ok"] = "Анкета сохранена. Добро пожаловать в FitMind!";
        return RedirectToAction("Index", "Schedule");
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        await FillAll();
        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == Uid);
        if (profile == null) return RedirectToAction(nameof(Setup));

        var formats = await db.UserTrainingFormats
            .Where(x => x.UserId == Uid)
            .Select(x => x.FormatId)
            .ToListAsync();
        return View(new ProfileEditVm
        {
            Name = profile.Name,
            Age = profile.Age,
            HeightCm = profile.HeightCm,
            WeightKg = profile.WeightKg,
            Gender = profile.Gender ?? "Не указан",
            ActivityLevelId = profile.ActivityLevelId,
            AboutMe = profile.AboutMe,
            GoalId = profile.GoalId,
            TrainingLevelId = profile.TrainingLevelId,
            AtHome = profile.AtHome,
            Outdoor = profile.Outdoor,
            AtGym = profile.AtGym,
            WorkoutsPerWeek = profile.WorkoutsPerWeek,
            FormatIds = formats
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProfileEditVm vm)
    {
        await FillAll();
        await ValidateReferences(activity:vm.ActivityLevelId,goal:vm.GoalId,level:vm.TrainingLevelId);
        vm.FormatIds ??= new List<int>();
        await ValidateReferences(formats:vm.FormatIds);
        if (!vm.AtHome && !vm.Outdoor && !vm.AtGym)
            ModelState.AddModelError(nameof(vm.AtHome), "Выберите хотя бы одно место тренировок.");
        if (vm.FormatIds.Count == 0)
            ModelState.AddModelError(nameof(vm.FormatIds), "Выберите хотя бы один формат тренировок.");
        if (!ModelState.IsValid) return View(vm);

        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == Uid);
        if (profile == null) return RedirectToAction(nameof(Setup));

        profile.Name = vm.Name.Trim();
        profile.Age = vm.Age;
        profile.HeightCm = vm.HeightCm;
        profile.WeightKg = vm.WeightKg;
        profile.Gender = vm.Gender;
        profile.ActivityLevelId = vm.ActivityLevelId;
        profile.AboutMe = string.IsNullOrWhiteSpace(vm.AboutMe) ? null : vm.AboutMe.Trim();
        profile.TrainingLevelId = vm.TrainingLevelId;
        profile.AtHome = vm.AtHome;
        profile.Outdoor = vm.Outdoor;
        profile.AtGym = vm.AtGym;
        profile.GoalId = vm.GoalId;
        profile.WorkoutsPerWeek = vm.WorkoutsPerWeek;
        profile.OnboardingCompleted = true;
        profile.OnboardingStep = 4;

        var oldFormats = await db.UserTrainingFormats.Where(x => x.UserId == Uid).ToListAsync();
        db.UserTrainingFormats.RemoveRange(oldFormats);
        foreach (var id in vm.FormatIds.Distinct())
            db.UserTrainingFormats.Add(new UserTrainingFormat { UserId = Uid, FormatId = id, CreatedAt = DateTime.Now });

        await db.SaveChangesAsync();
        TempData["Ok"] = "Профиль сохранён.";
        return RedirectToAction(nameof(Index));
    }

    async Task<UserProfile> GetOrCreateProfile()
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == Uid);
        if (profile != null) return profile;

        profile = new UserProfile
        {
            UserId = Uid,
            Name = User.Identity?.Name?.Split('@')[0] ?? "Пользователь",
            Gender = "Не указан",
            CreatedAt = DateTime.Now,
            OnboardingStep = 1,
            OnboardingCompleted = false
        };
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    IActionResult RedirectToCurrentStep(UserProfile? profile)
    {
        var step = profile?.OnboardingStep ?? 1;
        return step switch
        {
            2 => RedirectToAction(nameof(Goal)),
            3 => RedirectToAction(nameof(TrainingPreferences)),
            _ => RedirectToAction(nameof(Setup))
        };
    }

    async Task ValidateReferences(int? activity=null,int? goal=null,int? level=null,List<int>? formats=null){
        if(activity.HasValue&&!await db.ActivityLevels.AnyAsync(x=>x.Id==activity))ModelState.AddModelError("ActivityLevelId","Выберите существующий уровень активности.");
        if(goal.HasValue&&!await db.FitnessGoals.AnyAsync(x=>x.Id==goal))ModelState.AddModelError("GoalId","Выберите существующую цель.");
        if(level.HasValue&&!await db.TrainingLevels.AnyAsync(x=>x.Id==level))ModelState.AddModelError("TrainingLevelId","Выберите существующий уровень.");
        if(formats!=null&&await db.TrainingFormats.CountAsync(x=>formats.Contains(x.Id))!=formats.Distinct().Count())ModelState.AddModelError("FormatIds","Выберите форматы из списка.");
    }
    async Task FillActivities() =>
        ViewBag.Activities = (await db.ActivityLevels.ToListAsync()).OrderBy(x => x.Coefficient).ToList();

    async Task FillGoalsAndLevels()
    {
        // Все цели анкеты из таблицы «Цели».
        ViewBag.Goals = await db.FitnessGoals.OrderBy(x => x.Id).ToListAsync();
        ViewBag.Levels = await db.TrainingLevels.OrderBy(x => x.Id).ToListAsync();
    }

    async Task FillTrainingPreferences()
    {
        ViewBag.Formats = await db.TrainingFormats.OrderBy(x => x.Id).ToListAsync();
    }

    async Task FillAll()
    {
        await FillActivities();
        await FillGoalsAndLevels();
        await FillTrainingPreferences();
    }
}
