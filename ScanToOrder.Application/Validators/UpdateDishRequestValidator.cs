using FluentValidation;
using ScanToOrder.Application.DTOs.Dishes;

namespace ScanToOrder.Application.Validators
{
    public class UpdateDishRequestValidator : AbstractValidator<UpdateDishRequest>
    {
        public UpdateDishRequestValidator()
        {
            When(x => x.DishName != null, () =>
            {
                RuleFor(x => x.DishName)
                    .Must(name => !string.IsNullOrWhiteSpace(name))
                    .MaximumLength(100);
            });

            When(x => x.Price.HasValue, () =>
            {
                RuleFor(x => x.Price!.Value)
                    .GreaterThan(0);
            });

            When(x => x.Description != null, () =>
            {
                RuleFor(x => x.Description)
                    .NotEmpty();
            });

            When(x => x.DishAvailability.HasValue, () =>
            {
                RuleFor(x => x.DishAvailability!.Value)
                    .GreaterThanOrEqualTo(0);
            });

            RuleFor(x => x)
                .Must(x =>
                    x.DishName != null ||
                    x.Price.HasValue ||
                    x.Description != null ||
                    x.ImageUrl != null ||
                    x.DishAvailability.HasValue
                )
                .WithMessage("Phải cập nhật ít nhất một trường.");
        }
    }
}

