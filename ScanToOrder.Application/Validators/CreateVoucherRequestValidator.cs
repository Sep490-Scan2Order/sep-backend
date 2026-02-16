using FluentValidation;
using ScanToOrder.Application.DTOs.Voucher;

namespace ScanToOrder.Application.Validators
{
    public class CreateVoucherRequestValidator : AbstractValidator<CreateVoucherDto>
    {
        public CreateVoucherRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.DiscountValue)
                .GreaterThan(0);

            RuleFor(x => x.MinOrderAmount)
                .GreaterThanOrEqualTo(0);

            RuleFor(x => x.PointRequire)
                .GreaterThanOrEqualTo(0);
        }
    }
}
