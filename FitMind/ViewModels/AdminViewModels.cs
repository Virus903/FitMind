using System.ComponentModel.DataAnnotations;

namespace FitMind.ViewModels;

public sealed class AdminDashboardVm
{
    public int Users { get; init; }
    public int Exercises { get; init; }
    public int Programs { get; init; }
    public int Products { get; init; }
    public int Recipes { get; init; }
    public int Supplements { get; init; }
    public int Articles { get; init; }
    public IReadOnlyList<AdminAuditRowVm> RecentActions { get; init; } = [];
}

public sealed record AdminAuditRowVm(DateTime OccurredAt, string Action, string EntityType, string? EntityId, string? Summary, string? Actor);

public sealed class AdminContentVm
{
    public string Kind { get; set; } = "";
    public int? Id { get; set; }
    [Required(ErrorMessage = "Введите название.")]
    [StringLength(180)] public string Name { get; set; } = "";
    [StringLength(1200)] public string? Description { get; set; }
    public int? CategoryId { get; set; }
    [StringLength(500)] public string? Image { get; set; }
    public IFormFile? ImageFile { get; set; }
    public bool RemoveImage { get; set; }
    public bool Active { get; set; } = true;
    [StringLength(120)] public string? Meta { get; set; }
    [StringLength(6000)] public string? Details { get; set; }
    [Range(0.01, 100000)] public decimal? ServingGrams { get; set; }
    [Range(0, 10000)] public decimal? Calories { get; set; }
    [Range(0, 1000)] public decimal? Protein { get; set; }
    [Range(0, 1000)] public decimal? Fat { get; set; }
    [Range(0, 1000)] public decimal? Carbs { get; set; }
    [Range(0, 1000)] public decimal? Fiber { get; set; }
    [Range(0, 200)] public short? GlycemicIndex { get; set; }
    [Range(1, 1440)] public short? PreparationMinutes { get; set; }
    [StringLength(3000)] public string? Tips { get; set; }
    public List<int> MuscleIds { get; set; } = [];
    public int? PrimaryMuscleId { get; set; }
}

public sealed record AdminListRowVm(int Id, string Name, string? Category, bool Active, string? Image, string? Description);

public sealed class AdminListVm
{
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Query { get; init; }
    public string Status { get; init; } = "all";
    public string Sort { get; init; } = "name";
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalItems { get; init; }
    public IReadOnlyList<AdminListRowVm> Rows { get; init; } = [];
}

public sealed class AdminDictionaryVm
{
    public string Kind { get; set; } = "";
    public int? Id { get; set; }
    [Required(ErrorMessage = "Введите название.")]
    [StringLength(120)] public string Name { get; set; } = "";
    [StringLength(800)] public string? Description { get; set; }
}
