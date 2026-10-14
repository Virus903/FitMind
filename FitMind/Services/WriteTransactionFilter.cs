using FitMind.Data;
using Microsoft.AspNetCore.Mvc.Filters;
namespace FitMind.Services;
// Одна отправка формы — одна транзакция, даже если контроллер вызывает SaveChanges несколько раз.
public class WriteTransactionFilter(ApplicationDbContext db):IAsyncActionFilter
{
 public async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next) {
  if(!HttpMethods.IsPost(context.HttpContext.Request.Method)){await next();return;}
  await using var tx=await db.Database.BeginTransactionAsync();
  var result=await next();
  if(result.Exception==null)await tx.CommitAsync();
 }
}
