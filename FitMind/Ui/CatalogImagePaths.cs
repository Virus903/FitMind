namespace FitMind.Ui;

public static class CatalogImagePaths
{
    public const string TrainingPrograms = "/images/placeholders/training-programs-dark.png";
    public const string NutritionPrograms = "/images/placeholders/nutrition-programs-dark.png";
    public const string SleepPrograms = "/images/placeholders/sleep-programs-dark.png";
    public const string Exercises = "/images/placeholders/exercises-dark.png";
    public const string Food = "/images/placeholders/food-dark.png";
    public const string Recovery = "/images/placeholders/recovery-dark.png";
    public const string Supplements = "/images/placeholders/supplements-dark.png";
    public const string Knowledge = "/images/placeholders/knowledge-dark.png";

    public static (string Dark, string Light) For(string kind) => kind switch
    {
        nameof(TrainingPrograms) => (TrainingPrograms, "/images/placeholders/training-programs-light.png"),
        nameof(NutritionPrograms) => (NutritionPrograms, "/images/placeholders/nutrition-programs-light.png"),
        nameof(SleepPrograms) => (SleepPrograms, "/images/placeholders/sleep-programs-light.png"),
        nameof(Exercises) => (Exercises, "/images/placeholders/exercises-light.png"),
        nameof(Food) => (Food, "/images/placeholders/food-light.png"),
        nameof(Recovery) => (Recovery, "/images/placeholders/recovery-light.png"),
        nameof(Supplements) => (Supplements, "/images/placeholders/supplements-light.png"),
        nameof(Knowledge) => (Knowledge, "/images/placeholders/knowledge-light.png"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный вид заглушки.")
    };
}
