using ScanToOrder.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class RefundRequest
    {
        public Guid OrderId { get; set; }
        public RefundType RefundType { get; set; }
        public Guid ResponsibleStaffId { get; set; }
        public string? Note { get; set; }
        public IFormFile? ImageFile { get; set; }

        [FromForm]
        [ModelBinder(BinderType = typeof(FormDataJsonBinder))]
        public List<RefundItemDto> RefundItems { get; set; } = new();

        public bool IsFullRefund { get; set; } = true;
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

            var list = new List<RefundItemDto>();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            foreach (var value in valueResult.Values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;

                try
                {
                    var trimmed = value.TrimStart();

                    if (trimmed.StartsWith("["))
                    {
                        var arr = JsonSerializer.Deserialize<List<RefundItemDto>>(trimmed, options);
                        if (arr != null) list.AddRange(arr);
                    }
                    else
                    {
                        var single = JsonSerializer.Deserialize<RefundItemDto>(trimmed, options);
                        if (single != null) list.Add(single);
                    }
                }
                catch
                {
                    // ignore lỗi từng item
                }
            }

            // lọc dữ liệu hợp lệ
            list = list
                .Where(x => x.OrderDetailId > 0 && x.QuantityToRefund > 0)
                .ToList();

            bindingContext.Result = ModelBindingResult.Success(list);
            return Task.CompletedTask;
        }
    }
}