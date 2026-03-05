using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Enums
{
    [Flags]
    public enum DaysOfWeek
    {
        None = 0,
        Sunday = 1 << 0,    // 1 
        Monday = 1 << 1,    // 2 
        Tuesday = 1 << 2,   // 4
        Wednesday = 1 << 3, // 8 
        Thursday = 1 << 4,  // 16
        Friday = 1 << 5,    // 32 
        Saturday = 1 << 6,  // 64 
        
        Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday, // 62
        Weekend = Saturday | Sunday, // 65
        All = 127
    }
}
