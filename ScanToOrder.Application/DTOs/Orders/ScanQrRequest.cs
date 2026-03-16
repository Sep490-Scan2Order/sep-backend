using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class ScanQrRequest
    {
        public string QrContent { get; set; } = null!;
    }
}
