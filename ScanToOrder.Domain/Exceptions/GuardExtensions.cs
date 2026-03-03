using System.Diagnostics.CodeAnalysis;

namespace ScanToOrder.Domain.Exceptions
{
    public static class GuardExtensions
    {
        [return: NotNull]
        public static T OrThrow<T>(this T? value, string message) where T : class
        {
            if (value is null) throw new DomainException(message);
            return value;
        }
    }
}
