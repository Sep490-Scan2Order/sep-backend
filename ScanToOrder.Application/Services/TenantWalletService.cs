using System.Globalization;
using System.Text;
using ScanToOrder.Application.DTOs.External;
using ScanToOrder.Application.DTOs.Payment;
using ScanToOrder.Application.DTOs.Wallet;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Wallet;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class TenantWalletService : ITenantWalletService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly IAuthenticatedUserService _authenticatedUserService;
    private readonly IBankLookupService _bankLookupService;

    public TenantWalletService(IUnitOfWork unitOfWork, IPaymentService paymentService,
        IAuthenticatedUserService authenticatedUserService, IBankLookupService bankLookupService)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _authenticatedUserService = authenticatedUserService;
        _bankLookupService = bankLookupService;
    }

    public async Task<string> CreateDepositUrlAsync(decimal amount, NoteWalletTransaction note)
    {
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var tenantId = _authenticatedUserService.ProfileId;
        if (tenantId == null)
        {
            throw new UnauthorizedAccessException("Token không hợp lệ hoặc đã hết hạn.");
        }

        var tenantWallet = await _unitOfWork.TenantWallets.GetByTenantIdAsync(tenantId.Value);
        if (tenantWallet == null || tenantWallet.IsBlocked)
        {
            throw new Exception("Ví của Tenant không hợp lệ hoặc bị khóa");
        }

        var transaction = new WalletTransaction
        {
            TenantWalletId = tenantWallet.Id,
            Amount = amount,
            OrderCode = orderCode,
            TransactionStatus = TransactionStatus.Pending,
            TransactionType = TransactionType.Add,
            WalletType = WalletType.Tenant,
            Note = note,
        };

        await _unitOfWork.WalletTransactions.AddAsync(transaction);
        await _unitOfWork.SaveAsync();

        var request = new CreatePaymentRequest
        {
            Amount = (long)amount,
            OrderCode = orderCode,
            Description = $"Deposit Action",
            CancelUrl = "https://yourapp.com/payment/cancel",
            ReturnUrl = "https://www.youtube.com/",
        };
        var checkoutUrl = await _paymentService.CreatePaymentLinkAsync(request);
        return checkoutUrl;
    }

    public async Task<bool> HandleDepositWebhookAsync(object rawWebhook)
    {
        var result = await _paymentService.VerifyWebhookAsync(rawWebhook);

        if (result.OrderCode == 123) return true;

        if (!result.IsPaymentSuccess) return true;

        await using var tx = await _unitOfWork.BeginTransactionAsync();

        try
        {
            var walletTx = await _unitOfWork.WalletTransactions.GetByOrderCode(result.OrderCode);

            if (walletTx == null || walletTx.TransactionStatus != TransactionStatus.Pending)
            {
                return true;
            }
            
            if (walletTx.TenantWalletId == null) throw new Exception("Giao dịch không gắn với ví nào");

            var wallet = await _unitOfWork.TenantWallets.GetByIdAsync(walletTx.TenantWalletId.Value);

            if (wallet == null || wallet.IsBlocked)
                throw new Exception("Ví của Tenant không tồn tại hoặc bị khóa");
            
            if (walletTx.Note == NoteWalletTransaction.AccountVerification)
            {
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(wallet.TenantId);
    
                var tenantName = RemoveVietnameseSigns(tenant!.Name).ToUpper().Trim();

                var ownerBank = await _unitOfWork.Banks.GetByBinAsync(int.Parse(result.BankBin));
                var bankLookupResult = await _bankLookupService.LookupAccountAsync(new BankLookRequest()
                {
                    Bank = ownerBank.Code,
                    Account = result.AccountNumber
                });
                var bankOwnerName = RemoveVietnameseSigns(bankLookupResult.Data.OwnerName).ToUpper().Trim();

                if (tenantName.Equals(bankOwnerName))
                {
                    tenant.IsVerifyTax = true; 
                    _unitOfWork.Tenants.Update(tenant);
                    await _unitOfWork.SaveAsync();
                }
            }
            
            walletTx.BalanceBefore = wallet.WalletBalance;
            wallet.WalletBalance += walletTx.Amount;
            walletTx.BalanceAfter = wallet.WalletBalance;

            walletTx.TransactionStatus = TransactionStatus.Success;
            walletTx.PaymentDate = DateTime.UtcNow;
            walletTx.UpdatedAt = DateTime.UtcNow;

            wallet.UpdatedAt = DateTime.UtcNow;
            
            _unitOfWork.WalletTransactions.Update(walletTx);
            _unitOfWork.TenantWallets.Update(wallet);
            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();

            return true;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return false;
        }
    }

    public async Task <TenantWalletDto> CreateWalletTenantAsync(Guid tenantId)
    {
        var existingWallet = await _unitOfWork.TenantWallets.GetByTenantIdAsync(tenantId);
        if (existingWallet != null)
        {
            throw new Exception("Tenant đã có ví.");
        }

        var wallet = new TenantWallet
        {
            TenantId = tenantId,
            WalletBalance = 0,
            IsBlocked = false
        };

        await _unitOfWork.TenantWallets.AddAsync(wallet);
        await _unitOfWork.SaveAsync();

        return new TenantWalletDto
        {
            Id = wallet.Id,
            TenantId = wallet.TenantId,
            WalletBalance = wallet.WalletBalance,
            IsBlocked = wallet.IsBlocked
        };
    }
    
    private string RemoveVietnameseSigns(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return str;

        string normalizedString = str.Normalize(NormalizationForm.FormD);
        StringBuilder stringBuilder = new StringBuilder();

        foreach (char c in normalizedString)
        {
            UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'D');
    }
}