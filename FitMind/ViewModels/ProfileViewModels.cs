using System.ComponentModel.DataAnnotations;
using FitMind.Models;

namespace FitMind.ViewModels;

public class ProfileSetupVm
{
    [Required, Range(14, 100, ErrorMessage = "Укажите возраст от 14 до 100 лет.")]
    public byte? Age { get; set; }

    [Required, Range(100, 250, ErrorMessage = "Укажите рост от 100 до 250 см.")]
    public short? HeightCm { get; set; }

    [Required, Range(typeof(decimal), "30", "350", ErrorMessage = "Укажите вес от 30 до 350 кг.")]
    public decimal? WeightKg { get; set; }

    [Required] public string Gender { get; set; } = "Не указан";
    [Required(ErrorMessage = "Выберите уровень активности.")] public int? ActivityLevelId { get; set; }

    [StringLength(1000, ErrorMessage = "Максимум 1000 символов.")]
    public string? AboutMe { get; set; }
}

public class GoalStepVm
{
    [Required(ErrorMessage = "Выберите цель.")] public int? GoalId { get; set; }
    [Required(ErrorMessage = "Выберите уровень подготовки.")] public int? TrainingLevelId { get; set; }
}

public class TrainingPreferencesVm
{
    // Места тренировок: можно отметить несколько.
    public bool AtHome { get; set; }
    public bool Outdoor { get; set; }
    public bool AtGym { get; set; }
    [Required(ErrorMessage = "Выберите частоту тренировок.")] public byte? WorkoutsPerWeek { get; set; }
    public List<int> FormatIds { get; set; } = new();
}

public class ProfileEditVm
{
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, Range(14, 100)] public byte? Age { get; set; }
    [Required, Range(100, 250)] public short? HeightCm { get; set; }
    [Required, Range(typeof(decimal), "30", "350")] public decimal? WeightKg { get; set; }
    [Required] public string Gender { get; set; } = "Не указан";
    [Required] public int? ActivityLevelId { get; set; }
    [StringLength(1000)] public string? AboutMe { get; set; }

    [Required] public int? GoalId { get; set; }
    [Required] public int? TrainingLevelId { get; set; }
    public bool AtHome { get; set; }
    public bool Outdoor { get; set; }
    public bool AtGym { get; set; }
    [Required] public byte? WorkoutsPerWeek { get; set; }
    public List<int> FormatIds { get; set; } = new();
}

public class ProfilePageVm
{
    public UserProfile Profile { get; set; } = new();
    public string? GoalName { get; set; }
    public List<TrainingFormat> Formats { get; set; } = new();
    public List<FitnessProgram> ActivePrograms { get; set; } = new();
}

// Оставлен для совместимости со старой ссылкой /Profile/Questionnaire.
public class QuestionnaireVm : ProfileSetupVm { }
