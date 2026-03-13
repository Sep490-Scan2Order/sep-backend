using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json;

namespace ScanToOrder.Application.DTOs.Dishes
{
    public class CreateComboRequest
    {
        public string ComboName { get; set; } = null!;
        public decimal Price { get; set; }
        public string Description { get; set; } = null!;
        public IFormFile ImageUrl { get; set; } = null!;

        [FromForm]
        [ModelBinder(BinderType = typeof(FormDataJsonBinder))]
        public List<ComboItemRequest> Items { get; set; } = new List<ComboItemRequest>();
    }

    public class FormDataJsonBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            var list = new List<ComboItemRequest>();
            foreach (var value in valueResult.Values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;

                try
                {
                    if (value.TrimStart().StartsWith("["))
                    {
                        var arr = JsonSerializer.Deserialize<List<ComboItemRequest>>(value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (arr != null) list.AddRange(arr);
                    }
                    else
                    {
                        var single = JsonSerializer.Deserialize<ComboItemRequest>(value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (single != null) list.Add(single);
                    }
                }
                catch
                {
                    // Ignore parsing errors for individual items
                }
            }

            bindingContext.Result = ModelBindingResult.Success(list);
            return Task.CompletedTask;
        }
    }
}
