using ScanToOrder.Application.DTOs.SystemBlog;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Interfaces;
using System.Text.Json;

namespace ScanToOrder.Application.Services
{
    public class SystemBlogService : ISystemBlogService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorageService _storageService;
        public SystemBlogService(IUnitOfWork unitOfWork, IStorageService storageService)
        {
            _unitOfWork = unitOfWork;
            _storageService = storageService;
        }
        public async Task<AddSystemBlogDtoResponse> AddSystemBlogAsync(AddSystemBlogDtoRequest request)
        {
            var imageUrls = new List<string>();

            if (request.Images != null && request.Images.Any())
            {
                foreach (var file in request.Images)
                {
                    if (file.Length > 0)
                    {
                        using var ms = new MemoryStream();
                        await file.CopyToAsync(ms);
                        var fileBytes = ms.ToArray();

                        string fileName = $"blog_{Guid.NewGuid():N}_{file.FileName}";

                        string url = await _storageService.UploadQrCodeFromBytesAsync(fileBytes, fileName, "s2o_blog");
                        imageUrls.Add(url);
                    }
                }
            }

            var systemBlog = new SystemBlog
            {
                Content = request.Content,
                Title = request.Title,
                ColorTitle = request.ColorTitle,
                ImageUrl = JsonSerializer.Serialize(imageUrls),
                BlogType = request.BlogType,
            };

            await _unitOfWork.SystemBlogs.AddAsync(systemBlog);
            await _unitOfWork.SaveAsync();

            return new AddSystemBlogDtoResponse
            {
                SystemBlogId = systemBlog.SystemBlogId,
                Content = systemBlog.Content,
                Title = systemBlog.Title,
                ColorTitle = systemBlog.ColorTitle,
                ImageUrl = systemBlog.ImageUrl,
                BlogType = systemBlog.BlogType,
                CreatedAt = systemBlog.CreatedAt
            };
        }

        public async Task<IEnumerable<SystemBlogDto>> GetSystemBlogAsync()
        {
            var blogs = await _unitOfWork.SystemBlogs.GetAllAsync();
            return blogs.Select(blog => new SystemBlogDto
            {
                SystemBlogId = blog.SystemBlogId,
                Title = blog.Title,
                CreatedAt = blog.CreatedAt,
                UpdatedAt = blog.UpdatedAt,
                TotalViews = blog.TotalViews,
                BlogType = blog.BlogType
            });
        }

        public async Task<BlogDetailDto> GetSystemBlogByIdAsync(int systemBlogId)
        {
            var blog = await _unitOfWork.SystemBlogs.GetByIdAsync(systemBlogId);
            if (blog == null)
                throw new Exception(BlogMessage.BlogError.BLOG_NOT_FOUND);
            return new BlogDetailDto
            {
                Title = blog.Title,
                ColorTitle = blog.ColorTitle,
                Content = blog.Content,
                ImageUrl = blog.ImageUrl,
                CreatedAt = blog.CreatedAt,
                UpdatedAt = blog.UpdatedAt,
                TotalViews = blog.TotalViews,
                BlogType = blog.BlogType
            };
        }
    }
}
