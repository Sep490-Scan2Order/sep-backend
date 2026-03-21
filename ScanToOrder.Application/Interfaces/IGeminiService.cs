using ScanToOrder.Application.DTOs.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces
{
    public interface IGeminiService
    {
        Task<AiHolidayVisualDto> GenerateHolidayVisualConfigAsync(string holidayName);
    }
}
