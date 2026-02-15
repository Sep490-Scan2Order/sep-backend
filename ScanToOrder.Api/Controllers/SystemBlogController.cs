using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.SystemBlog;
using ScanToOrder.Application.Interfaces;

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
        public async Task<IActionResult> AddSystemBlog([FromBody] AddSystemBlogDtoRequest request)
        {
            var response = await _systemBlogService.AddSystemBlogAsync(request);
            return Ok(response);
        }
        [HttpGet]
        public async Task<IActionResult> GetSystemBlogs()
        {
            var blogs = await _systemBlogService.GetSystemBlogAsync();
            return Ok(blogs);
        }
    }
}
