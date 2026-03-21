using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces
{
    public interface IHuggingFaceService
    {
        Task<byte[]> GenerateImageBytesAsync(string prompt, int width = 512, int height = 1024);
    }
}
