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
        public async Task<ActionResult<ApiResponse<AddSystemBlogDtoResponse>>> AddSystemBlog([FromBody] AddSystemBlogDtoRequest request)
        {
            var response = await _systemBlogService.AddSystemBlogAsync(request);
            return Success(response);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<SystemBlog>>>> GetSystemBlogs()
        {
            var blogs = await _systemBlogService.GetSystemBlogAsync();
            return Success(blogs);
        }
    }
}
