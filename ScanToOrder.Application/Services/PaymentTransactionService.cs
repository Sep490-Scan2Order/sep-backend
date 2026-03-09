using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class PaymentTransactionService
{
    private readonly IUnitOfWork _unitOfWork;

    public PaymentTransactionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    
}