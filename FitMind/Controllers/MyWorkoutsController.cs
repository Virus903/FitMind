using FitMind.Data;
using FitMind.Models;
using FitMind.Services;
using FitMind.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitMind.Controllers;

/// <summary>
/// «Мои тренировки» (шаг 5): программы (таблица «Программы»), личные упражнения («Упражнения» с владельцем),
/// записи дня («Записи_дня») и подходы («Подходы»). Сюда же перенесены страницы WorkoutManager.
/// </summary>
[Authorize]
public class MyWorkoutsController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
{
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private const string DefaultProgramCover = "/images/placeholders/training-programs-dark.png";
    private const string DefaultExerciseCover = "/images/placeholders/exercises-dark.png";

    IQueryable<FitnessProgram> MyPrograms => db.Programs.Where(x => x.OwnerId == Uid && x.Kind == ProgramKind.Workout);
    IQueryable<DayEntry> MyWorkouts => db.DayEntries.Where(x => x.UserId == Uid && x.Type == ProgramItemType.Workout);

    public async Task<IActionResult> Index(DateTime? month)
    {
        var m = month.HasValue ? new DateTime(month.Value.Year, month.Value.Month, 1) : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var weekStart = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        await ProgramScheduleService.EnsureActivePlannedAsync(db, Uid);
        var programs = await MyPrograms.Include(x => x.Items).OrderByDescending(x => x.Active).ThenByDescending(x => x.CreatedAt).ToListAsync();
        var today = DateTime.Today;
        var todayWorkout = await MyWorkouts.Where(x => x.Start >= today && x.Start < today.AddDays(1) && x.Status != DayEntryStatus.Skipped)
            .Include(x => x.Sets).ThenInclude(x => x.Exercise)
            .Include(x => x.ProgramItem).ThenInclude(x => x!.Exercises).ThenInclude(x => x.Exercise)
            .OrderBy(x => x.Status).ThenBy(x => x.Start).FirstOrDefaultAsync();
        return View(new MyWorkoutsRedesignVm
        {
            Month = m,
            WeekStart = weekStart,
            CalendarWorkouts = await MyWorkouts.Where(x => x.Start >= m.AddDays(-7) && x.Start < m.AddMonths(1).AddDays(7)).OrderBy(x => x.Start).ToListAsync(),
            TodayWorkout = todayWorkout,
            ActiveProgram = programs.FirstOrDefault(x => x.Active),
            Programs = programs,
            Exercises = await db.Exercises.Where(x => x.OwnerId == Uid && x.Active).OrderBy(x => x.Name).ToListAsync(),
            History = await MyWorkouts.Where(x => x.Status == DayEntryStatus.Done).Include(x => x.Sets).OrderByDescending(x => x.Start).Take(8).ToListAsync(),
            Notes = await db.PersonalNotes.Where(x => x.UserId == Uid && x.Area == "Тренировки").OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync()
        });
    }

    // ---------- Программы ----------

    public async Task<IActionResult> ViewProgram(int id)
    {
        var p = await MyPrograms.Include(x => x.Level).Include(x => x.Items).ThenInclude(x => x.Exercises).ThenInclude(x => x.Exercise).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        ViewBag.Own = true;
        return View("~/Views/Programs/Details.cshtml", p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetProgramActive(int id, bool active = true)
    {
        var p = await MyPrograms.Include(x => x.Items).ThenInclude(x => x.Exercises).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        if (active && !p.Items.Any(x => x.Exercises.Count > 0))
        { TempData["Error"] = "Сначала настройте упражнения программы."; return RedirectToAction(nameof(EditProgram), new { id }); }
        if (active && !p.Items.Any(x => x.Time.HasValue))
        { TempData["Error"] = "Сначала настройте дни недели и время программы."; return RedirectToAction(nameof(EditProgram), new { id }); }
        await using var transaction = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync() : null;
        if (active)
        {
            foreach (var other in await MyPrograms.Where(x => x.Active && x.Id != id).ToListAsync())
            { other.Active = false; await ProgramScheduleService.UnplanFutureAsync(db, other); }
            p.Active = true;
            if (!p.StartDate.HasValue || ProgramScheduleService.ProgramEnd(p) < DateTime.Today)
            { p.StartDate = DateTime.Today; p.EndDate = DateTime.Today.AddDays((p.Weeks ?? 8) * 7 - 1); }
            await db.SaveChangesAsync();
            var created = await ProgramScheduleService.PlanAsync(db, p);
            TempData["Ok"] = $"Программа активна. Запланировано тренировок: {created}.";
        }
        else
        {
            p.Active = false;
            await db.SaveChangesAsync();
            var removed = await ProgramScheduleService.UnplanFutureAsync(db, p);
            TempData["Ok"] = $"Программа выключена. Убрано будущих тренировок: {removed}; прошлые записи остались.";
        }
        if (transaction != null) await transaction.CommitAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateProgram()
    {
        await FillProgramExercises();
        return View(new UserProgramEditVm { Days = [new ProgramDayEditVm { Name = "День 1", DayOfWeek = 1 }] });
    }

    [HttpGet]
    public async Task<IActionResult> EditProgram(int id)
    {
        var p = await MyPrograms.Include(x => x.Items).ThenInclude(x => x.Exercises).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        await FillProgramExercises();
        return View("CreateProgram", new UserProgramEditVm
        {
            Id = p.Id, Name = p.Name, StartDate = p.StartDate ?? DateTime.Today, Description = p.Description, GoalName = p.GoalText,
            Weeks = p.Weeks ?? 8, WorkoutsPerWeek = p.WorkoutsPerWeek ?? 3, Image = p.Image,
            Days = p.Items.Where(x => x.Type == ProgramItemType.Workout).OrderBy(x => x.DayNumber).ThenBy(x => x.Order).Select(d => new ProgramDayEditVm
            {
                Id = d.Id, Name = d.Title, DayOfWeek = (int)(d.DayOfWeek ?? DayOfWeek.Monday), Time = d.Time ?? new TimeSpan(18, 0, 0),
                Exercises = d.Exercises.OrderBy(e => e.Order).Select(e => new ProgramExerciseEditVm { ExerciseId = e.ExerciseId, Sets = e.Sets, Reps = e.RepsMax ?? e.RepsMin, DurationSeconds = e.Seconds, RestSeconds = e.RestSeconds, WorkingWeightKg = e.WeightKg, Comment = e.Comment }).ToList()
            }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProgram(UserProgramEditVm vm)
    {
        await FillProgramExercises();
        vm.Days ??= [];
        foreach (var day in vm.Days) day.Exercises ??= [];
        if (vm.Days.Count is < 1 or > 7) ModelState.AddModelError("", "Добавьте от 1 до 7 тренировочных дней.");
        if (vm.Days.Any(x => x.Exercises.Count > 50)) ModelState.AddModelError("", "В одном дне допускается не более 50 упражнений.");
        if (vm.Days.Any(x => x.Time < TimeSpan.Zero || x.Time >= TimeSpan.FromDays(1))) ModelState.AddModelError("", "Укажите время от 00:00 до 23:59.");
        if (vm.StartDate.Year < 2000 || vm.StartDate.Year > 2100) ModelState.AddModelError(nameof(vm.StartDate), "Выберите дату старта между 2000 и 2100 годами.");
        if (vm.Days.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Exercises.Count == 0)) ModelState.AddModelError("", "У каждого дня должны быть название и хотя бы одно упражнение.");
        if (vm.Days.GroupBy(x => x.DayOfWeek).Any(x => x.Count() > 1)) ModelState.AddModelError("", "Для каждого дня недели можно задать только одну тренировку.");
        var exerciseIds = vm.Days.SelectMany(x => x.Exercises).Select(x => x.ExerciseId).Distinct().ToList();
        if (exerciseIds.Count == 0 || await AvailableExercises().CountAsync(x => exerciseIds.Contains(x.Id)) != exerciseIds.Count) ModelState.AddModelError("", "Выбрано недоступное упражнение.");
        var cover = await ContentImageStorage.SaveAsync(vm.CoverFile, environment);
        if (cover.Error != null) ModelState.AddModelError(nameof(vm.CoverFile), cover.Error);
        if (!string.IsNullOrWhiteSpace(vm.Image) && !IsAllowedCover(vm.Image)) ModelState.AddModelError(nameof(vm.Image), "Выберите обложку из списка или загрузите картинку.");
        if (!ModelState.IsValid) return View(vm);

        await using var transaction = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync() : null;
        FitnessProgram p;
        if (vm.Id.HasValue)
        {
            p = await MyPrograms.Include(x => x.Items).ThenInclude(x => x.Exercises).FirstOrDefaultAsync(x => x.Id == vm.Id) ?? null!;
            if (p == null) return NotFound();
            // Будущие запланированные записи пересоздаются по новому расписанию; прошлые остаются (ссылка на пункт обнуляется).
            await ProgramScheduleService.UnplanFutureAsync(db, p);
            db.ProgramItems.RemoveRange(p.Items);
            p.Items.Clear();
        }
        else
        {
            p = new FitnessProgram { Kind = ProgramKind.Workout, OwnerId = Uid, CreatedAt = DateTime.Now };
            db.Programs.Add(p);
        }
        p.Name = vm.Name.Trim(); p.Description = vm.Description; p.GoalText = vm.GoalName; p.Weeks = vm.Weeks; p.WorkoutsPerWeek = (byte)vm.Days.Count;
        p.StartDate = vm.StartDate.Date; p.EndDate = vm.StartDate.Date.AddDays(vm.Weeks * 7 - 1);
        p.Image = cover.Path ?? (string.IsNullOrWhiteSpace(vm.Image) ? p.Image ?? DefaultProgramCover : vm.Image);
        for (var dayNo = 0; dayNo < vm.Days.Count; dayNo++)
        {
            var source = vm.Days[dayNo];
            var item = new ProgramItem { DayNumber = (byte)(dayNo + 1), DayOfWeek = (DayOfWeek)source.DayOfWeek, Time = source.Time, Type = ProgramItemType.Workout, Title = source.Name.Trim(), Order = (short)(dayNo + 1) };
            byte order = 1;
            foreach (var e in source.Exercises)
                item.Exercises.Add(new ProgramItemExercise { ExerciseId = e.ExerciseId, Order = order++, Sets = e.Sets, RepsMin = e.Reps, RepsMax = e.Reps, Seconds = e.DurationSeconds, RestSeconds = e.RestSeconds, WeightKg = e.WorkingWeightKg, Comment = e.Comment });
            p.Items.Add(item);
        }
        await db.SaveChangesAsync();
        if (p.Active) await ProgramScheduleService.PlanAsync(db, p);
        if (transaction != null) await transaction.CommitAsync();
        TempData["Ok"] = "Программа и расписание сохранены.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProgram(int id)
    {
        var p = await MyPrograms.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        await using var transaction = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync() : null;
        await ProgramScheduleService.UnplanFutureAsync(db, p);
        db.Programs.Remove(p); // пункты удаляются каскадом, у прошлых записей ссылка на пункт обнуляется
        await db.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        TempData["Ok"] = "Программа удалена. Выполненные тренировки остались в записях.";
        return RedirectToAction(nameof(Index));
    }

    // Обложка: загрузка (как аватар в ProfileController.UploadAvatar) или выбор из /images/covers.
    [HttpGet, AllowAnonymous]
    public IActionResult Cover(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(c => !char.IsLetterOrDigit(c))) return NotFound();
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "program-covers", name + ".img");
        if (!System.IO.File.Exists(path)) return NotFound();
        var data = System.IO.File.ReadAllBytes(path);
        return File(data, data[0] == 0x89 ? "image/png" : data[0] == 0xff ? "image/jpeg" : "image/webp");
    }

    async Task<(string? Path, string? Error)> SaveCoverAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0) return (null, null);
        if (file.Length < 12 || file.Length > 2 * 1024 * 1024) return (null, "Обложка: PNG, JPEG или WebP размером до 2 МБ.");
        using var stream = new MemoryStream(); await file.CopyToAsync(stream); var data = stream.ToArray();
        var png = data.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = data[0] == 255 && data[1] == 216 && data[2] == 255;
        var webp = System.Text.Encoding.ASCII.GetString(data, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(data, 8, 4) == "WEBP";
        if (!(png || jpeg || webp)) return (null, "Файл обложки не является PNG, JPEG или WebP.");
        var name = Guid.NewGuid().ToString("N");
        var dir = Path.Combine(environment.ContentRootPath, "App_Data", "program-covers");
        Directory.CreateDirectory(dir);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, name + ".img"), data);
        return (Url.Action(nameof(Cover), new { name }) ?? $"/MyWorkouts/Cover?name={name}", null);
    }

    bool IsAllowedCover(string image) =>
        image.StartsWith("/MyWorkouts/Cover", StringComparison.Ordinal)
        || (image.StartsWith("/images/covers/", StringComparison.Ordinal) && !image.Contains("..")
            && System.IO.File.Exists(Path.Combine(environment.WebRootPath, image.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));

    async Task FillProgramExercises()
    {
        ViewBag.MyExercises = await db.Exercises.Where(x => x.OwnerId == Uid && x.Active).OrderBy(x => x.Name).ToListAsync();
        ViewBag.Exercises = await db.Exercises.Where(x => x.OwnerId == null && x.Active).Include(x => x.Category).OrderBy(x => x.Name).ToListAsync();
        ViewBag.Covers = Directory.Exists(Path.Combine(environment.WebRootPath, "images", "covers"))
            ? Directory.GetFiles(Path.Combine(environment.WebRootPath, "images", "covers"), "programs-*.svg").Select(x => "/images/covers/" + Path.GetFileName(x)).OrderBy(x => x.Length).ThenBy(x => x).ToList()
            : new List<string>();
    }

    // База FitMind и свои упражнения пользователя.
    IQueryable<Exercise> AvailableExercises() => db.Exercises.Where(x => x.Active && (x.OwnerId == null || x.OwnerId == Uid));

    // ---------- Мои упражнения ----------

    [HttpGet]
    public IActionResult CreateExercise() => View(new UserExerciseEditVm());

    [HttpGet]
    public async Task<IActionResult> EditExercise(int id)
    {
        var e = await db.Exercises.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == Uid);
        if (e == null) return NotFound();
        return View("CreateExercise", new UserExerciseEditVm { Id = e.Id, Name = e.Name, Description = e.Description, Equipment = e.Equipment, Difficulty = e.Difficulty, Image = e.Image, Technique = e.Technique, MuscleGroup = e.Place });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExercise(UserExerciseEditVm vm)
    {
        if (!string.IsNullOrWhiteSpace(vm.Image) && !IsAllowedCover(vm.Image)) ModelState.AddModelError(nameof(vm.Image), "Выберите картинку из /images/covers или загрузите файл.");
        var upload = await ContentImageStorage.SaveAsync(vm.ImageFile, environment);
        if (upload.Error != null) ModelState.AddModelError(nameof(vm.ImageFile), upload.Error);
        if (!ModelState.IsValid) return View(vm);
        Exercise e;
        if (vm.Id.HasValue)
        {
            e = await db.Exercises.FirstOrDefaultAsync(x => x.Id == vm.Id && x.OwnerId == Uid) ?? null!;
            if (e == null) return NotFound();
        }
        else
        {
            var categoryId = await db.Categories.Where(x=>x.Section=="Упражнения").OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
            e = new Exercise { OwnerId = Uid, CategoryId = categoryId, Active = true };
            db.Exercises.Add(e);
        }
        e.Name = vm.Name.Trim(); e.Description = vm.Description; e.Equipment = vm.Equipment; e.Difficulty = vm.Difficulty ?? "";
        e.Technique = vm.Technique; e.Place = vm.MuscleGroup;
        e.Image = upload.Path ?? (string.IsNullOrWhiteSpace(vm.Image) ? e.Image ?? DefaultExerciseCover : vm.Image);
        await db.SaveChangesAsync();
        TempData["Ok"] = vm.Id.HasValue ? "Упражнение сохранено." : "Своё упражнение создано.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ViewExercise(int id)
    {
        var e = await db.Exercises.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == Uid);
        if (e == null) return NotFound();
        if (e.SourceExerciseId.HasValue) return RedirectToAction("Details", "Exercises", new { id = e.SourceExerciseId });
        return View("ExerciseDetails", e);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExerciseFromBase(int id)
    {
        if (await CopyBaseExerciseAsync(db, Uid, id) == null) return NotFound();
        TempData["Ok"] = "Упражнение добавлено в «Мои упражнения» с картинкой и описанием.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Копия упражнения базы в «Мои упражнения» (Упражнения с ID_владельца).</summary>
    internal static async Task<Exercise?> CopyBaseExerciseAsync(ApplicationDbContext db, string uid, int id)
    {
        var e = await db.Exercises.FirstOrDefaultAsync(x => x.Id == id && x.Active && x.OwnerId == null);
        if (e == null) return null;
        var existing = await db.Exercises.FirstOrDefaultAsync(x => x.OwnerId == uid && x.SourceExerciseId == id);
        if (existing != null) { existing.Active = true; await db.SaveChangesAsync(); return existing; }
        var copy = new Exercise
        {
            OwnerId = uid, SourceExerciseId = e.Id, CategoryId = e.CategoryId, Name = e.Name, Description = e.Description, Technique = e.Technique,
            CommonMistakes = e.CommonMistakes, Recommendations = e.Recommendations, Contraindications = e.Contraindications, Equipment = e.Equipment,
            Place = e.Place, Difficulty = e.Difficulty, CaloriesPerHour = e.CaloriesPerHour, Image = e.Image, Video = e.Video, Active = true
        };
        db.Exercises.Add(copy);
        await db.SaveChangesAsync();
        return copy;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExercise(int id)
    {
        var e = await db.Exercises.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == Uid);
        if (e == null) return NotFound();
        // Удаляется только из «Моих». Если упражнение уже есть в программах или записях — скрывается, история сохраняется.
        var used = await db.ProgramItemExercises.AnyAsync(x => x.ExerciseId == id) || await db.EntrySets.AnyAsync(x => x.ExerciseId == id);
        if (used) e.Active = false; else db.Exercises.Remove(e);
        await db.SaveChangesAsync();
        TempData["Ok"] = "Упражнение убрано из «Моих упражнений».";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Тренировки (записи дня) ----------

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartToday(long id)
    {
        var entry = await MyWorkouts.Include(x => x.Sets).FirstOrDefaultAsync(x => x.Id == id);
        if (entry == null) return NotFound();
        await ProgramScheduleService.EnsureSetsAsync(db, entry);
        if (entry.Status == DayEntryStatus.Planned && entry.Start.Date == DateTime.Today && !entry.Sets.Any(s => s.Done)) { entry.Start = DateTime.Now; await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Run), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? exerciseId = null)
    {
        await FillExercises();
        var vm = new WorkoutCreateVm { StartAt = DateTime.Now.AddHours(1), PlannedDurationMinutes = 60 };
        if (exerciseId.HasValue) vm.ExerciseIds.Add(exerciseId.Value);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorkoutCreateVm vm)
    {
        await FillExercises();
        vm.ExerciseIds ??= [];
        vm.CustomExercises ??= [];
        var ids = vm.ExerciseIds.Distinct().ToList();
        if (await AvailableExercises().CountAsync(x => ids.Contains(x.Id)) != ids.Count) ModelState.AddModelError("", "Одно из упражнений недоступно.");
        var custom = vm.CustomExercises.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        if (ids.Count == 0 && custom.Count == 0) ModelState.AddModelError("", "Добавьте хотя бы одно упражнение.");
        if (custom.Any(x => x.Length > 160)) ModelState.AddModelError("", "Название своего упражнения — до 160 символов.");
        if (!ModelState.IsValid) return View(vm);
        // Упражнение «только для этой тренировки» сохраняется как скрытое личное упражнение.
        foreach (var name in custom)
        {
            var categoryId = await db.Categories.Where(x=>x.Section=="Упражнения").OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
            var own = new Exercise { OwnerId = Uid, CategoryId = categoryId, Name = name, Difficulty = "", Image = DefaultExerciseCover, Active = false };
            db.Exercises.Add(own); await db.SaveChangesAsync(); ids.Add(own.Id);
        }
        var w = new DayEntry { UserId = Uid, Type = ProgramItemType.Workout, Title = vm.Name.Trim(), Start = vm.StartAt, Value = vm.PlannedDurationMinutes, Comment = vm.Note, Status = DayEntryStatus.Planned };
        foreach (var id in ids) for (short n = 1; n <= 3; n++) w.Sets.Add(new EntrySet { ExerciseId = id, Number = n });
        db.DayEntries.Add(w);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Run), new { id = w.Id });
    }

    async Task<DayEntry?> OwnedEntry(long id) => await MyWorkouts.Include(x => x.Sets).ThenInclude(x => x.Exercise).ThenInclude(x => x!.Category).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);

    static List<RunExerciseVm> Group(DayEntry w) => w.Sets.GroupBy(x => x.ExerciseId).OrderBy(g => g.Min(s => s.Id))
        .Select(g => new RunExerciseVm { Exercise = g.First().Exercise!, Sets = g.OrderBy(s => s.Number).ToList() }).ToList();

    public async Task<IActionResult> Run(long id)
    {
        var w = await OwnedEntry(id);
        if (w == null) return NotFound();
        if (w.Sets.Count == 0 && w.ProgramItemId.HasValue) { await ProgramScheduleService.EnsureSetsAsync(db, w); w = await OwnedEntry(id); }
        return View(new WorkoutRunVm { Workout = w!, Exercises = Group(w!), StatusText = ProgramScheduleService.StatusText(w!) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Begin(long id)
    {
        var w = await MyWorkouts.Include(x => x.Sets).FirstOrDefaultAsync(x => x.Id == id);
        if (w == null) return NotFound();
        await ProgramScheduleService.EnsureSetsAsync(db, w);
        if (w.Status == DayEntryStatus.Planned && !w.Sets.Any(s => s.Done)) { w.Start = DateTime.Now; await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Run), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSet(long setId, decimal? weightKg, short? reps, short? durationSeconds, bool completed)
    {
        var set = await db.EntrySets.Include(x => x.DayEntry).FirstOrDefaultAsync(x => x.Id == setId && x.DayEntry!.UserId == Uid);
        if (set == null) return NotFound();
        if (!ModelState.IsValid || weightKg < 0 || weightKg > 2000 || reps < 0 || durationSeconds < 0)
        { TempData["Error"] = "Введите корректные неотрицательные значения подхода."; return RedirectToAction(nameof(Run), new { id = set.DayEntryId }); }
        set.WeightKg = weightKg; set.Reps = reps; set.Seconds = durationSeconds; set.Done = completed;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Run), new { id = set.DayEntryId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSet(long entryId, int exerciseId)
    {
        var w = await MyWorkouts.Include(x => x.Sets).FirstOrDefaultAsync(x => x.Id == entryId);
        if (w == null || !w.Sets.Any(x => x.ExerciseId == exerciseId)) return NotFound();
        w.Sets.Add(new EntrySet { ExerciseId = exerciseId, Number = (short)(w.Sets.Where(x => x.ExerciseId == exerciseId).Max(x => x.Number) + 1) });
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Run), new { id = entryId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Finish(long id, byte? wellbeing, string? note)
    {
        var w = await MyWorkouts.FirstOrDefaultAsync(x => x.Id == id);
        if (w == null) return NotFound();
        if (!ModelState.IsValid || wellbeing < 1 || wellbeing > 5) return BadRequest("Самочувствие: от 1 до 5.");
        if (w.Status == DayEntryStatus.Done) return RedirectToAction(nameof(Index));
        if (w.Start > DateTime.Now) w.Start = DateTime.Now;
        w.Status = DayEntryStatus.Done; w.End = DateTime.Now; w.Rating = wellbeing; w.Comment = note;
        await db.SaveChangesAsync();
        TempData["Ok"] = "Тренировка сохранена в записи.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> AddRecord(long? workoutId)
    {
        var vm = new WorkoutRecordVm();
        if (workoutId.HasValue)
        {
            var w = await MyWorkouts.FirstOrDefaultAsync(x => x.Id == workoutId);
            if (w != null)
                vm = new WorkoutRecordVm { WorkoutId = w.Id, Name = w.Title, Date = w.Start.Date, Time = w.Start.TimeOfDay, DurationMinutes = Math.Max(1, (int)((w.End ?? DateTime.Now) - w.Start).TotalMinutes), Wellbeing = w.Rating, Note = w.Comment };
        }
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRecord(WorkoutRecordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        DayEntry w;
        if (vm.WorkoutId.HasValue)
        {
            w = await MyWorkouts.FirstOrDefaultAsync(x => x.Id == vm.WorkoutId) ?? null!;
            if (w == null) return NotFound();
        }
        else { w = new DayEntry { UserId = Uid, Type = ProgramItemType.Workout }; db.DayEntries.Add(w); }
        w.Title = vm.Name.Trim(); w.Start = vm.Date.Date.Add(vm.Time); w.End = w.Start.AddMinutes(vm.DurationMinutes);
        w.Value = vm.DurationMinutes; w.Status = DayEntryStatus.Done; w.Rating = vm.Wellbeing; w.Comment = vm.Note;
        await db.SaveChangesAsync();
        TempData["Ok"] = "Запись тренировки добавлена.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> History()
    {
        var list = await MyWorkouts.Include(x => x.Sets).OrderByDescending(x => x.Start).ToListAsync();
        return View(list);
    }

    // ---------- Бывший WorkoutManager: просмотр, изменение, «в мой день», удаление ----------

    public async Task<IActionResult> Details(long id)
    {
        var w = await OwnedEntry(id);
        if (w == null) return NotFound();
        if (w.Sets.Count == 0 && w.ProgramItemId.HasValue) { await ProgramScheduleService.EnsureSetsAsync(db, w); w = await OwnedEntry(id); }
        ViewBag.Goal = await db.UserProfiles.Where(x => x.UserId == Uid && x.GoalId != null).Select(x => x.Goal!.Name).FirstOrDefaultAsync();
        ViewBag.Level = await db.UserProfiles.Where(x => x.UserId == Uid).Select(x => x.TrainingLevel!.Name).FirstOrDefaultAsync();
        return View(new WorkoutRunVm { Workout = w!, Exercises = Group(w!), StatusText = ProgramScheduleService.StatusText(w!) });
    }

    [HttpGet]
    public async Task<IActionResult> EditWorkout(long id)
    {
        var w = await OwnedEntry(id);
        if (w == null) return NotFound();
        if (w.Sets.Count == 0 && w.ProgramItemId.HasValue) { await ProgramScheduleService.EnsureSetsAsync(db, w); w = await OwnedEntry(id); }
        var vm = new WorkoutManageVm
        {
            Id = id, Name = w!.Title, Note = w.Comment, Duration = w.Value.HasValue ? (short)w.Value.Value : null,
            Lines = Group(w).Select(x => new WorkoutLineVm { ExerciseId = x.Exercise.Id, Sets = (short)Math.Max(1, x.Sets.Count), Reps = x.Sets.FirstOrDefault()?.Reps, Weight = x.Sets.FirstOrDefault()?.WeightKg, Seconds = x.Sets.FirstOrDefault()?.Seconds }).ToList()
        };
        await WorkoutCatalog(vm, w);
        return View(vm);
    }

    async Task WorkoutCatalog(WorkoutManageVm vm, DayEntry w)
    {
        var used = w.Sets.Select(x => x.ExerciseId).Distinct().ToList();
        vm.Catalog = await db.Exercises.Where(x => (x.Active && (x.OwnerId == null || x.OwnerId == Uid)) || used.Contains(x.Id))
            .OrderBy(x => x.OwnerId == null).ThenBy(x => x.Name).ToListAsync();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditWorkout(WorkoutManageVm vm)
    {
        var w = await OwnedEntry(vm.Id);
        if (w == null) return NotFound();
        vm.Lines ??= [];
        if (vm.Lines.Count == 0 || vm.Lines.Count > 50) ModelState.AddModelError("", "Добавьте от 1 до 50 упражнений.");
        var ids = vm.Lines.Where(x => x.ExerciseId.HasValue).Select(x => x.ExerciseId!.Value).Distinct().ToList();
        var used = w.Sets.Select(x => x.ExerciseId).Distinct().ToList();
        var valid = await db.Exercises.Where(x => ids.Contains(x.Id) && ((x.Active && (x.OwnerId == null || x.OwnerId == Uid)) || used.Contains(x.Id))).Select(x => x.Id).ToListAsync();
        if (vm.Lines.Any(x => !x.ExerciseId.HasValue) || ids.Except(valid).Any()) ModelState.AddModelError("", "Выберите упражнение из списка.");
        if (vm.Lines.Where(x => x.ExerciseId.HasValue).GroupBy(x => x.ExerciseId).Any(g => g.Count() > 1)) ModelState.AddModelError("", "Каждое упражнение можно добавить один раз.");
        if (!ModelState.IsValid) { await WorkoutCatalog(vm, w); return View(vm); }
        w.Title = vm.Name.Trim(); w.Comment = vm.Note; w.Value = vm.Duration;
        var old = w.Sets.ToList();
        db.EntrySets.RemoveRange(old.Where(s => !ids.Contains(s.ExerciseId)));
        foreach (var line in vm.Lines)
        {
            var sets = old.Where(s => s.ExerciseId == line.ExerciseId).OrderBy(s => s.Number).ToList();
            foreach (var extra in sets.Where(s => s.Number > line.Sets)) db.EntrySets.Remove(extra);
            for (short n = 1; n <= line.Sets; n++)
            {
                var set = sets.FirstOrDefault(s => s.Number == n);
                if (set == null) { set = new EntrySet { ExerciseId = line.ExerciseId!.Value, Number = n }; w.Sets.Add(set); }
                set.Reps = line.Reps; set.WeightKg = line.Weight; set.Seconds = line.Seconds;
            }
        }
        await db.SaveChangesAsync();
        TempData["Ok"] = "Тренировка сохранена.";
        return RedirectToAction(nameof(Details), new { id = w.Id });
    }

    [HttpGet]
    public async Task<IActionResult> AddToDay(long id)
    {
        var w = await OwnedEntry(id);
        if (w == null) return NotFound();
        ViewBag.StatusText = ProgramScheduleService.StatusText(w);
        return View(w);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToDay(long id, DateTime date, TimeSpan time, bool copy = false)
    {
        var w = await OwnedEntry(id);
        if (w == null) return NotFound();
        if (date.Year < 2000 || date.Year > 2100 || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) return BadRequest();
        if (copy)
        {
            var clone = new DayEntry { UserId = Uid, Type = ProgramItemType.Workout, Title = w.Title, Comment = w.Comment, Value = w.Value, Start = date.Date.Add(time), Status = DayEntryStatus.Planned };
            foreach (var s in w.Sets) clone.Sets.Add(new EntrySet { ExerciseId = s.ExerciseId, Number = s.Number, Reps = s.Reps, WeightKg = s.WeightKg, Seconds = s.Seconds });
            db.DayEntries.Add(clone); await db.SaveChangesAsync(); w = clone;
        }
        else
        {
            if (w.Status == DayEntryStatus.Done) { TempData["Error"] = "Завершённую тренировку можно повторить копией."; return RedirectToAction(nameof(AddToDay), new { id }); }
            w.Start = date.Date.Add(time);
        }
        w.Status = DayEntryStatus.Planned;
        await db.SaveChangesAsync();
        TempData["Ok"] = "Тренировка добавлена в Мой день.";
        return RedirectToAction("Index", "Schedule", new { date = date.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteWorkout(long id, bool fromIndex = false)
    {
        var w = await MyWorkouts.FirstOrDefaultAsync(x => x.Id == id);
        if (w == null) return NotFound();
        var links = new[] { "/Schedule/Edit/" + id };
        db.Notifications.RemoveRange(await db.Notifications.Where(x => x.UserId == Uid && links.Contains(x.Link!)).ToListAsync());
        if (w.ProgramItemId.HasValue && w.Status == DayEntryStatus.Planned)
        {
            // Запись программы не удаляется, а помечается пропущенной, чтобы расписание не создало её снова.
            w.Status = DayEntryStatus.Skipped;
            db.EntrySets.RemoveRange(await db.EntrySets.Where(x => x.DayEntryId == id && !x.Done).ToListAsync());
        }
        else db.DayEntries.Remove(w);
        await db.SaveChangesAsync();
        TempData["Ok"] = "Тренировка удалена.";
        return RedirectToAction(fromIndex ? nameof(Index) : nameof(History));
    }

    // ---------- Заметки раздела ----------

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNote(long? id, string text, string? title)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 4000 || title?.Length > 200) { TempData["Error"] = "Текст заметки: от 1 до 4000 символов."; return RedirectToAction(nameof(Index)); }
        PersonalNote? note;
        if (id.HasValue)
        {
            note = await db.PersonalNotes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid && x.Area == "Тренировки");
            if (note == null) return NotFound();
        }
        else { note = new PersonalNote { UserId = Uid, Area = "Тренировки", CreatedAt = DateTime.Now }; db.PersonalNotes.Add(note); }
        note.Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(); note.Text = text.Trim();
        await db.SaveChangesAsync();
        TempData["Ok"] = "Заметка сохранена.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNote(long id)
    {
        var note = await db.PersonalNotes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid && x.Area == "Тренировки");
        if (note == null) return NotFound();
        db.PersonalNotes.Remove(note); await db.SaveChangesAsync();
        TempData["Ok"] = "Заметка удалена.";
        return RedirectToAction(nameof(Index));
    }

    async Task FillExercises()
    {
        ViewBag.MyExercises = await db.Exercises.Where(x => x.OwnerId == Uid && x.Active).OrderBy(x => x.Name).ToListAsync();
        ViewBag.Exercises = await db.Exercises.Where(x => x.Active && x.OwnerId == null).Include(x => x.Category).OrderBy(x => x.Name).ToListAsync();
    }
}
