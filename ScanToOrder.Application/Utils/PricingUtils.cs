using System;

namespace ScanToOrder.Application.Utils
{
    public static class PricingUtils
    {
        public static int RoundToNearestThousand(decimal discountedPrice)
        {
            return (int)(Math.Round(discountedPrice / 1000m, MidpointRounding.AwayFromZero) * 1000m);
        }
    }
}
