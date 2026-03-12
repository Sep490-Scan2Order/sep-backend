using System;
using System.Globalization;
using System.Text;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.Utils;

public static class BankQrLinkUtils
{
    private const string SePayBaseUrl = "https://qr.sepay.vn/img";
    private const string PaymentPrefix = "SToO"; 
    
    private const int MaxSuffixLength = 10; 
    
    private static readonly Random _random = new Random();
    
    private static string GenerateNumericString(int length)
    {
        var builder = new StringBuilder(length);
        
        builder.Append(_random.Next(1, 10)); 
        
        for (int i = 1; i < length; i++)
        {
            builder.Append(_random.Next(0, 10));
        }
        
        return builder.ToString();
    }

    public static (string QrUrl, string PaymentCode) GenerateSePayQrUrl(
        string account, 
        string bank, 
        decimal amount, 
        PaymentIntent intent)
    {
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(bank))
        {
            throw new ArgumentException("Số tài khoản và ngân hàng không được để trống.");
        }

        string intentSuffix = intent == PaymentIntent.OrderPayment ? "ORD" : "VER";

        int numericLength = MaxSuffixLength - intentSuffix.Length;

        string numericPart = GenerateNumericString(numericLength);

        string paymentCode = $"{PaymentPrefix}{numericPart}{intentSuffix}";

        string formattedAmount = Math.Round(amount).ToString("0");
        string encodedDes = Uri.EscapeDataString(paymentCode);
        
        string qrUrl = $"{SePayBaseUrl}?acc={account}&bank={bank}&amount={formattedAmount}&des={encodedDes}";

        return (qrUrl, paymentCode);
    }
    
    public static PaymentIntent? DetectPaymentIntent(string paymentCode)
    {
        if (string.IsNullOrWhiteSpace(paymentCode))
            return null;

        if (!paymentCode.StartsWith(PaymentPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        if (paymentCode.EndsWith("ORD", StringComparison.OrdinalIgnoreCase))
            return PaymentIntent.OrderPayment;

        if (paymentCode.EndsWith("VER", StringComparison.OrdinalIgnoreCase))
            return PaymentIntent.TenantVerification;

        return null;
    }
    
    public static long GeneratePayOsOrderCode()
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int randomSuffix = _random.Next(0, 1000);
        string combined = $"{timestamp}{randomSuffix:D3}";
        return long.Parse(combined);
    }
    
    public static string RemoveVietnameseTones(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        string normalizedString = text.Normalize(NormalizationForm.FormD);
        StringBuilder stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        string result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        return result.Replace('đ', 'd').Replace('Đ', 'D');
    }
}