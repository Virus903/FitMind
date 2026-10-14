namespace FitMind.ViewModels;
public class AddMealVm {
 public DateTime DateTime {get;set;}=DateTime.Now;
 public int ProductId {get;set;}
 public decimal Grams {get;set;}=100;
 public string? Comment {get;set;}
}
