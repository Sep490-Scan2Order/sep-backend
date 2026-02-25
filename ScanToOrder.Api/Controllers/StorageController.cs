using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Storage;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class StorageController : BaseController
    {
        private readonly IStorageService _storageService;
        public StorageController(IStorageService storageService)
        {
            _storageService = storageService;   
        }

        [HttpPost("upload-file")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<string>>> UploadFile([FromForm] UploadFileRequest request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(StorageMessage.StorageError.INVALID_FILE_TYPE);
            }

            using var ms = new MemoryStream();
            await request.File.CopyToAsync(ms);
            var fileBytes = ms.ToArray();

            var fileName = $"{Guid.NewGuid()}_{request.File.FileName}";

            var result = await _storageService.UploadQrCodeFromBytesAsync(fileBytes, fileName);
            return Success(result);
        }
    }
}
