using FitMind.Models;
using FitMind.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<FitnessGoal> FitnessGoals => Set<FitnessGoal>();
    public DbSet<TrainingLevel> TrainingLevels => Set<TrainingLevel>();
    public DbSet<ActivityLevel> ActivityLevels => Set<ActivityLevel>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<TrainingFormat> TrainingFormats => Set<TrainingFormat>();
    public DbSet<UserTrainingFormat> UserTrainingFormats => Set<UserTrainingFormat>();
    public DbSet<MuscleGroup> MuscleGroups => Set<MuscleGroup>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<FitnessProgram> Programs => Set<FitnessProgram>();
    public DbSet<ProgramItem> ProgramItems => Set<ProgramItem>();
    public DbSet<ProgramItemExercise> ProgramItemExercises => Set<ProgramItemExercise>();
    public DbSet<DayEntry> DayEntries => Set<DayEntry>();
    public DbSet<EntrySet> EntrySets => Set<EntrySet>();
    public DbSet<ExerciseMuscle> ExerciseMuscles => Set<ExerciseMuscle>();
    public DbSet<FoodProduct> FoodProducts => Set<FoodProduct>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<DayEntryFoodItem> DayEntryFoodItems => Set<DayEntryFoodItem>();
    public DbSet<MeasurementType> MeasurementTypes => Set<MeasurementType>();
    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();
    public DbSet<Supplement> Supplements => Set<Supplement>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<RecoveryMethod> RecoveryMethods => Set<RecoveryMethod>();
    public DbSet<PersonalNote> PersonalNotes => Set<PersonalNote>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        foreach(var entity in b.Model.GetEntityTypes())
        foreach(var property in entity.GetProperties())
            if(property.ClrType==typeof(decimal)||property.ClrType==typeof(decimal?)){property.SetPrecision(18);property.SetScale(4);}

        b.Entity<ApplicationUser>(e =>
        {
            e.ToTable("Пользователи");
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.UserName).HasColumnName("Имя_пользователя");
            e.Property(x => x.NormalizedUserName).HasColumnName("Нормализованное_имя_пользователя");
            e.Property(x => x.Email).HasColumnName("Электронная_почта");
            e.Property(x => x.NormalizedEmail).HasColumnName("Нормализованная_электронная_почта");
            e.Property(x => x.EmailConfirmed).HasColumnName("Почта_подтверждена");
            e.Property(x => x.PasswordHash).HasColumnName("Хэш_пароля");
            e.Property(x => x.SecurityStamp).HasColumnName("Метка_безопасности");
            e.Property(x => x.ConcurrencyStamp).HasColumnName("Метка_конкурентности");
            e.Property(x => x.PhoneNumber).HasColumnName("Номер_телефона");
            e.Property(x => x.PhoneNumberConfirmed).HasColumnName("Телефон_подтверждён");
            e.Property(x => x.TwoFactorEnabled).HasColumnName("Двухфакторная_аутентификация");
            e.Property(x => x.LockoutEnd).HasColumnName("Блокировка_до");
            e.Property(x => x.LockoutEnabled).HasColumnName("Блокировка_разрешена");
            e.Property(x => x.AccessFailedCount).HasColumnName("Неудачных_попыток_входа");
        });
        b.Entity<IdentityRole>(e =>
        {
            e.ToTable("Роли");
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Name).HasColumnName("Название");
            e.Property(x => x.NormalizedName).HasColumnName("Нормализованное_название");
            e.Property(x => x.ConcurrencyStamp).HasColumnName("Метка_конкурентности");
        });
        b.Entity<IdentityUserRole<string>>(e =>
        {
            e.ToTable("Пользователи_Роли");
            e.Property(x => x.UserId).HasColumnName("ID_пользователя");
            e.Property(x => x.RoleId).HasColumnName("ID_роли");
        });
        b.Entity<IdentityUserClaim<string>>(e =>
        {
            e.ToTable("Утверждения_пользователей");
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.UserId).HasColumnName("ID_пользователя");
            e.Property(x => x.ClaimType).HasColumnName("Тип_утверждения");
            e.Property(x => x.ClaimValue).HasColumnName("Значение_утверждения");
        });
        b.Entity<IdentityRoleClaim<string>>(e =>
        {
            e.ToTable("Утверждения_ролей");
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.RoleId).HasColumnName("ID_роли");
            e.Property(x => x.ClaimType).HasColumnName("Тип_утверждения");
            e.Property(x => x.ClaimValue).HasColumnName("Значение_утверждения");
        });
        b.Entity<IdentityUserLogin<string>>(e =>
        {
            e.ToTable("Внешние_входы");
            e.Property(x => x.LoginProvider).HasColumnName("Поставщик_входа");
            e.Property(x => x.ProviderKey).HasColumnName("Ключ_поставщика");
            e.Property(x => x.ProviderDisplayName).HasColumnName("Отображаемое_имя_поставщика");
            e.Property(x => x.UserId).HasColumnName("ID_пользователя");
        });
        b.Entity<IdentityUserToken<string>>(e =>
        {
            e.ToTable("Токены_пользователей");
            e.Property(x => x.UserId).HasColumnName("ID_пользователя");
            e.Property(x => x.LoginProvider).HasColumnName("Поставщик_входа");
            e.Property(x => x.Name).HasColumnName("Название");
            e.Property(x => x.Value).HasColumnName("Значение");
        });

        b.Entity<UserTrainingFormat>().HasKey(x => new { x.UserId, x.FormatId });
        b.Entity<ExerciseMuscle>().HasKey(x => new { x.ExerciseId, x.MuscleGroupId });

        b.Entity<UserProfile>().HasOne(x => x.TrainingLevel).WithMany().HasForeignKey(x => x.TrainingLevelId);
        b.Entity<UserProfile>().HasOne(x => x.ActivityLevel).WithMany().HasForeignKey(x => x.ActivityLevelId);
        b.Entity<UserProfile>().HasOne(x => x.Goal).WithMany().HasForeignKey(x => x.GoalId);
        b.Entity<UserTrainingFormat>().HasOne(x => x.Format).WithMany().HasForeignKey(x => x.FormatId);
        b.Entity<Exercise>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.Entity<Category>().Property(x => x.Section).HasMaxLength(450);
        b.Entity<Category>().Property(x => x.Name).HasMaxLength(450);
        b.Entity<ExerciseMuscle>().HasOne(x => x.Exercise).WithMany().HasForeignKey(x => x.ExerciseId);
        b.Entity<ExerciseMuscle>().HasOne(x => x.MuscleGroup).WithMany().HasForeignKey(x => x.MuscleGroupId);
        b.Entity<FoodProduct>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<FoodProduct>().Property(x => x.Kind).HasDefaultValue(FoodProductKind.Product);
        b.Entity<FoodProduct>().HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<FoodProduct>().HasOne(x => x.Source).WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<FoodProduct>().HasIndex(x => new { x.OwnerId, x.Kind });
        b.Entity<RecipeIngredient>().HasOne(x => x.Recipe).WithMany(x => x.Ingredients).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<RecipeIngredient>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<BodyMeasurement>().HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId);
        b.Entity<MeasurementType>().Property(x => x.IsMain).HasDefaultValue(false);
        b.Entity<Supplement>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.Entity<Article>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.Entity<AdminAuditLog>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<UserProfile>().HasIndex(x => x.UserId).IsUnique();
        // Шаг 5: общие таблицы программ и журнала дня.
        b.Entity<Exercise>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Exercise>().HasOne(x => x.SourceExercise).WithMany().HasForeignKey(x => x.SourceExerciseId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Exercise>().HasIndex(x => x.OwnerId);
        b.Entity<FitnessProgram>().HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<FitnessProgram>().HasOne(x => x.SourceProgram).WithMany().HasForeignKey(x => x.SourceProgramId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<FitnessProgram>().HasOne(x => x.Level).WithMany().HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<FitnessProgram>().HasIndex(x => new { x.Kind, x.OwnerId });
        b.Entity<ProgramItem>().HasOne(x => x.Program).WithMany(x => x.Items).HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProgramItem>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<ProgramItem>().HasOne(x => x.Supplement).WithMany().HasForeignKey(x => x.SupplementId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<ProgramItem>().HasOne(x => x.RecoveryMethod).WithMany().HasForeignKey(x => x.RecoveryMethodId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<ProgramItemExercise>().HasOne(x => x.ProgramItem).WithMany(x => x.Exercises).HasForeignKey(x => x.ProgramItemId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProgramItemExercise>().HasOne(x => x.Exercise).WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<DayEntry>().HasOne(x => x.ProgramItem).WithMany().HasForeignKey(x => x.ProgramItemId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<DayEntry>().HasOne(x => x.RecoveryMethod).WithMany().HasForeignKey(x => x.RecoveryMethodId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<DayEntry>().HasOne(x => x.Supplement).WithMany().HasForeignKey(x => x.SupplementId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Supplement>().HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Supplement>().HasOne(x => x.Source).WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<Supplement>().HasIndex(x => x.OwnerId);
        b.Entity<RecoveryMethod>().HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<RecoveryMethod>().HasOne(x => x.Source).WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<RecoveryMethod>().HasIndex(x => x.OwnerId);
        b.Entity<DayEntry>().HasIndex(x => new { x.UserId, x.Start });
        b.Entity<DayEntry>().HasIndex(x => new { x.ProgramItemId, x.Start });
        b.Entity<EntrySet>().HasOne(x => x.DayEntry).WithMany(x => x.Sets).HasForeignKey(x => x.DayEntryId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<EntrySet>().HasOne(x => x.Exercise).WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<DayEntryFoodItem>().HasOne(x => x.DayEntry).WithMany(x => x.FoodItems).HasForeignKey(x => x.DayEntryId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DayEntryFoodItem>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        // Владельцы личных данных существуют в Identity; индексы FK ускоряют выборку по пользователю.
        foreach(var entity in b.Model.GetEntityTypes().Where(e=>e.ClrType.Namespace=="FitMind.Models"&&e.FindProperty("UserId")!=null).ToList())
            b.Entity(entity.ClrType).HasOne(typeof(ApplicationUser),entity.ClrType==typeof(UserProfile)?nameof(UserProfile.User):null).WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.NoAction);
        b.Entity<BodyMeasurement>().HasIndex(x=>new{x.UserId,x.DateTime});
        b.Entity<AdminAuditLog>().HasIndex(x => new { x.ActorUserId, x.OccurredAt });
        b.Entity<AdminAuditLog>().HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt });
    }
}
