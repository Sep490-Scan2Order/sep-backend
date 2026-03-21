using System.Text.RegularExpressions;

namespace ScanToOrder.Application.Utils
{
    public static class ValidationUtils
    {
        public static bool IsValidPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            var regex = new Regex(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$");

            return regex.IsMatch(password);
        }
    }
}
