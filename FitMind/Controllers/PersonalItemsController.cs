using System.Globalization;
using System.Security.Claims;
using FitMind.Data;
using FitMind.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FitMind.Controllers;

public record PersonalField(string Key,string Label,string Type="text");
public class PersonalEditVm
{
 public string Kind {get;set;}="";
 public long Id {get;set;}
 public Dictionary<string,string?> Values {get;set;}=new();
 public List<PersonalField> Fields {get;set;}=new();
 public string Back {get;set;}="Schedule";
}
[Authorize]
public class PersonalItemsController(ApplicationDbContext db):Controller
{
 string Uid=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 static (Type Type,string Back,PersonalField[] Fields)? Schema(string kind)=>kind switch {
  "measurement"=>(typeof(BodyMeasurement),"Measurements",new[]{new PersonalField("DateTime","Дата и время","datetime-local"),new("Value","Значение","number"),new("Comment","Комментарий","textarea")}),
  "note"=>(typeof(PersonalNote),"Schedule",new[]{new PersonalField("Title","Заголовок"),new("Text","Текст","textarea")}),
  _=>null
 };
 async Task<object?> Owned(string kind,long id)
 {
  var schema=Schema(kind);if(schema==null)return null;
  var x=await db.FindAsync(schema.Value.Type,id);if(x==null)return null;
  return (string?)x.GetType().GetProperty("UserId")?.GetValue(x)==Uid?x:null;
 }
 [HttpGet]
 public async Task<IActionResult> Edit(string kind,long id)
 {
  var x=await Owned(kind,id);if(x==null)return NotFound();var s=Schema(kind)!.Value;
  return View(new PersonalEditVm{Kind=kind,Id=id,Back=s.Back,Fields=s.Fields.ToList(),Values=s.Fields.ToDictionary(f=>f.Key,f=>x.GetType().GetProperty(f.Key)!.GetValue(x) switch {DateTime d=>d.ToString("yyyy-MM-ddTHH:mm"),TimeSpan t=>t.ToString(@"hh\:mm"),IFormattable n=>n.ToString(null,CultureInfo.InvariantCulture),var v=>v?.ToString()})});
 }
 [HttpPost,ValidateAntiForgeryToken]
 public async Task<IActionResult> Edit(PersonalEditVm vm)
 {
  var x=await Owned(vm.Kind,vm.Id);if(x==null)return NotFound();var s=Schema(vm.Kind)!.Value;vm.Fields=s.Fields.ToList();vm.Back=s.Back;
  foreach(var f in s.Fields){
   var raw=vm.Values.GetValueOrDefault(f.Key)?.Trim();var prop=x.GetType().GetProperty(f.Key)!;var type=Nullable.GetUnderlyingType(prop.PropertyType)??prop.PropertyType;
   if((f.Key=="Name"||f.Key=="Text")&&string.IsNullOrWhiteSpace(raw)){ModelState.AddModelError("",$"Заполните поле «{f.Label}».");continue;}
   if(raw?.Length>2000){ModelState.AddModelError("",$"Поле «{f.Label}» слишком длинное.");continue;}
   try{
    object? value;
    if(type==typeof(string))value=raw;
    else if(string.IsNullOrEmpty(raw)&&Nullable.GetUnderlyingType(prop.PropertyType)!=null)value=null;
    else if(type==typeof(DateTime)){var d=DateTime.Parse(raw!,CultureInfo.InvariantCulture);if(d.Year<2000||d.Year>2100)throw new FormatException();value=d;}
    else if(type==typeof(TimeSpan)){var t=TimeSpan.Parse(raw!,CultureInfo.InvariantCulture);if(t<TimeSpan.Zero||t>=TimeSpan.FromDays(1))throw new FormatException();value=t;}
    else{var number=decimal.Parse(raw!.Replace(',','.'),CultureInfo.InvariantCulture);if(number<0||number>10000)throw new FormatException();value=Convert.ChangeType(number,type,CultureInfo.InvariantCulture);}
    prop.SetValue(x,value);
   }catch(Exception e) when(e is FormatException||e is OverflowException||e is ArgumentException){ModelState.AddModelError("",$"Проверьте поле «{f.Label}».");}
  }
  if(x is BodyMeasurement measurement && (measurement.Value<=0||measurement.Value>10000))ModelState.AddModelError("","Укажите положительное значение замера.");
  if(!ModelState.IsValid)return View(vm);
  await db.SaveChangesAsync();TempData["Ok"]="Изменения сохранены.";return RedirectToAction("Index",s.Back);
 }
 [HttpPost,ValidateAntiForgeryToken]
 public async Task<IActionResult> Delete(string kind,long id)
 {
  var x=await Owned(kind,id);if(x==null)return NotFound();var back=Schema(kind)!.Value.Back;
  db.Remove(x);await db.SaveChangesAsync();TempData["Ok"]="Запись удалена.";return RedirectToAction("Index",back);
 }
}
