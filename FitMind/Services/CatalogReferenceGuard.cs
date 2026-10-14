using FitMind.Data;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Services;

// Explicitly protects user history even where a legacy FK permits cascade or SET NULL.
public static class CatalogReferenceGuard
{
    public static async Task<bool> IsUsedAsync(ApplicationDbContext db, string kind, int id) => kind switch
    {
        "exercise-categories" => await db.Exercises.AnyAsync(x => x.CategoryId == id),
        "muscles" => await db.ExerciseMuscles.AnyAsync(x => x.MuscleGroupId == id),
        "food-categories" => await db.FoodProducts.AnyAsync(x => x.CategoryId == id),
        "supplement-categories" => await db.Supplements.AnyAsync(x => x.CategoryId == id),
        "article-categories" => await db.Articles.AnyAsync(x => x.CategoryId == id),
        "exercises" => await db.ProgramItemExercises.AnyAsync(x => x.ExerciseId == id)
            || await db.EntrySets.AnyAsync(x => x.ExerciseId == id)
            || await db.Exercises.AnyAsync(x => x.SourceExerciseId == id),
        "products" or "recipes" => await db.RecipeIngredients.AnyAsync(x => x.ProductId == id || x.RecipeId == id)
            || await db.DayEntryFoodItems.AnyAsync(x => x.ProductId == id)
            || await db.FoodProducts.AnyAsync(x => x.SourceId == id),
        "recovery" => await db.DayEntries.AnyAsync(x => x.RecoveryMethodId == id)
            || await db.ProgramItems.AnyAsync(x => x.RecoveryMethodId == id)
            || await db.RecoveryMethods.AnyAsync(x => x.SourceId == id),
        "supplements" => await db.ProgramItems.AnyAsync(x => x.SupplementId == id)
            || await db.DayEntries.AnyAsync(x => x.SupplementId == id)
            || await db.Supplements.AnyAsync(x => x.SourceId == id),
        _ => false
    };
}
