using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.SystemBlog;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Blogs;

namespace ScanToOrder.Api.Controllers
{
    public class SystemBlogController : BaseController
    {
        private readonly ISystemBlogService _systemBlogService;
        public SystemBlogController(ISystemBlogService systemBlogService)
        {
            _systemBlogService = systemBlogService;
        }
        [HttpPost]
        public async Task<ApiResponse<AddSystemBlogDtoResponse>> AddSystemBlog([FromBody] AddSystemBlogDtoRequest request)
        {
            var response = await _systemBlogService.AddSystemBlogAsync(request);
            return new ApiResponse<AddSystemBlogDtoResponse>
            {
                IsSuccess = response.IsSuccess,
                Data = response.Data,
                Message = response.Message,
                Timestamp = DateTime.UtcNow
            };
        }

        [HttpGet]
        public async Task<ApiResponse<IEnumerable<SystemBlog>>> GetSystemBlogs()
        {
            var blogs = await _systemBlogService.GetSystemBlogAsync();
            return new ApiResponse<IEnumerable<SystemBlog>>
            {
                IsSuccess = blogs.IsSuccess,
                Data = blogs.Data,
                Message = blogs.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
