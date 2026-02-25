using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ScanToOrder.Application.DTOs.Storage
{
    public class UploadFileRequest
    {
        [FromForm(Name = "file")]
        public IFormFile File { get; set; } = default!;
    }
}
