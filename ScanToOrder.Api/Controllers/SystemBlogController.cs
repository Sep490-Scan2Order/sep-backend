using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.DTOs.SystemBlog;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Enums;

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
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<AddSystemBlogDtoResponse>>> AddSystemBlog([FromForm] AddSystemBlogDtoRequest request)
        {
            var response = await _systemBlogService.AddSystemBlogAsync(request);
            return Success(response);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<SystemBlogDto>>>> GetSystemBlogs(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] BlogType? blogType = null)
        {
            var (items, totalCount) = await _systemBlogService.GetSystemBlogsAsync(pageIndex, pageSize, blogType);

            var pagedResult = new PagedResult<SystemBlogDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = pageIndex,
                PageSize = pageSize
            };

            return Success(pagedResult);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<BlogDetailDto>>> GetSystemBlogById(int id)
        {
            var blog = await _systemBlogService.GetSystemBlogByIdAsync(id);
            if (blog == null)
            {
                return NotFound(BlogMessage.BlogError.BLOG_NOT_FOUND);
            }
            return Success(blog);
        }
    }
}
