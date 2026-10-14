using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FitMind.Models.Identity;

namespace FitMind.Models;

public enum FoodProductKind { Product = 0, Recipe = 1 }

[Table("Продукты")]
public class FoodProduct
{
    [Key, Column("ID_продукта")] public int Id { get; set; }
    [Column("ID_категории")] public int CategoryId { get; set; }
    [Column("Вид")] public FoodProductKind Kind { get; set; }
    [Column("ID_владельца")] public string? OwnerId { get; set; }
    [Column("ID_источника")] public int? SourceId { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Описание")] public string? Description { get; set; }
    [Column("Изображение")] public string? Image { get; set; }
    [Column("Порция_г")] public decimal? ServingGrams { get; set; }
    [Column("Ккал_на_100г")] public decimal Calories { get; set; }
    [Column("Белки_на_100г")] public decimal Protein { get; set; }
    [Column("Жиры_на_100г")] public decimal Fat { get; set; }
    [Column("Углеводы_на_100г")] public decimal Carbs { get; set; }
    [Column("Клетчатка_на_100г")] public decimal? Fiber { get; set; }
    [Column("Гликемический_индекс")] public short? GlycemicIndex { get; set; }
    [Column("Польза")] public string? Benefits { get; set; }
    [Column("Советы_по_приготовлению")] public string? CookingTips { get; set; }
    [Column("Приготовление")] public string? Instructions { get; set; }
    [Column("Время_приготовления_мин")] public short? PreparationMinutes { get; set; }
    [Column("Активно")] public bool Active { get; set; }
    public ApplicationUser? Owner { get; set; }
    public FoodProduct? Source { get; set; }
    public Category? Category { get; set; }
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}

[Table("Состав_рецепта")]
public class RecipeIngredient
{
    [Key, Column("ID_строки")] public int Id { get; set; }
    [Column("ID_рецепта")] public int RecipeId { get; set; }
    [Column("ID_продукта")] public int? ProductId { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Граммы")] public decimal? Grams { get; set; }
    [Column("Порядок")] public short Order { get; set; }
    public FoodProduct? Recipe { get; set; }
    public FoodProduct? Product { get; set; }
}

[Table("Состав_приёма_пищи")]
public class DayEntryFoodItem
{
    [Key, Column("ID_строки")] public long Id { get; set; }
    [Column("ID_записи")] public long DayEntryId { get; set; }
    [Column("ID_продукта")] public int? ProductId { get; set; }
    [Column("Название")] public string? CustomName { get; set; }
    [Column("Масса_г")] public decimal Grams { get; set; }
    [Column("Ккал_на_100г")] public decimal? CaloriesPer100 { get; set; }
    [Column("Белки_на_100г")] public decimal? ProteinPer100 { get; set; }
    [Column("Жиры_на_100г")] public decimal? FatPer100 { get; set; }
    [Column("Углеводы_на_100г")] public decimal? CarbsPer100 { get; set; }
    public DayEntry? DayEntry { get; set; }
    public FoodProduct? Product { get; set; }
}
