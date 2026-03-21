using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;

namespace ScanToOrder.Application.DTOs.Restaurant
{
    public class InvariantNullableDoubleModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            var raw = valueResult.FirstValue;
            if (string.IsNullOrWhiteSpace(raw))
            {
                bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }

            // Try invariant first so values like 107.333179 are parsed correctly regardless of server locale.
            if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture,
                    out var invariantValue))
            {
                bindingContext.Result = ModelBindingResult.Success(invariantValue);
                return Task.CompletedTask;
            }

            // Fallback for input using comma decimal separator.
            var normalized = raw.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var normalizedValue))
            {
                bindingContext.Result = ModelBindingResult.Success(normalizedValue);
                return Task.CompletedTask;
            }

            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Invalid numeric format.");
            return Task.CompletedTask;
        }
    }
}