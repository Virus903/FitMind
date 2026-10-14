using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FitMind.Services;

// Принимает 72,5 и 72.5 независимо от языка Windows/браузера.
public sealed class FlexibleDecimalBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        (Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType) == typeof(decimal)
            ? new FlexibleDecimalBinder() : null;
}
public sealed class FlexibleDecimalBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, value);
        var text = value.FirstValue?.Trim().Replace(',', '.');
        if (string.IsNullOrEmpty(text) && Nullable.GetUnderlyingType(context.ModelType) != null)
            context.Result = ModelBindingResult.Success(null);
        else if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
            context.Result = ModelBindingResult.Success(number);
        else context.ModelState.TryAddModelError(context.ModelName, "Введите число, например 72,5.");
        return Task.CompletedTask;
    }
}
