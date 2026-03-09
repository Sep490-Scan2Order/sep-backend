using ScanToOrder.Application.DTOs.SystemBlog;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
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

                        string url = await _storageService.UploadFromBytesAsync(fileBytes, fileName, "s2o_blog");
                        imageUrls.Add(url);
                    }
                }
            }

            var thumbnailUrl = string.Empty;
            if (request.Thumbnail != null && request.Thumbnail.Length > 0)
            {
                using var ms = new MemoryStream();
                await request.Thumbnail.CopyToAsync(ms);
                var fileBytes = ms.ToArray();
                string fileName = $"blog_thumbnail_{Guid.NewGuid():N}_{request.Thumbnail.FileName}";
                thumbnailUrl = await _storageService.UploadFromBytesAsync(fileBytes, fileName, "thumbnail");
            }

            var systemBlog = new SystemBlog
            {
                Content = request.Content,
                Title = request.Title,
                ColorTitle = request.ColorTitle,
                ImageUrl = JsonSerializer.Serialize(imageUrls),
                BlogType = request.BlogType,
                ThumbnailUrl = thumbnailUrl,
                IsActive = true,
                IsDeleted = false,
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
                ThumbnailUrl = systemBlog.ThumbnailUrl,
                BlogType = systemBlog.BlogType,
                CreatedAt = systemBlog.CreatedAt,
                IsActive = systemBlog.IsActive == true,
                IsDeleted = systemBlog.IsDeleted,
            };
        }

        public async Task<(List<SystemBlogDto> Items, int TotalCount)> GetSystemBlogsAsync(
            int pageIndex, int pageSize, BlogType? blogType)
        {
            var (blogs, totalCount) = await _unitOfWork.SystemBlogs
                .GetSystemBlogsSortByCreatedDateAsync(pageIndex, pageSize, blogType);

            var blogDtos = blogs.Select(blog => new SystemBlogDto
            {
                SystemBlogId = blog.SystemBlogId,
                Title = blog.Title,
                CreatedAt = blog.CreatedAt,
                UpdatedAt = blog.UpdatedAt,
                TotalViews = blog.TotalViews,
                BlogType = blog.BlogType,
                IsActive = blog.IsActive,
                IsDeleted = blog.IsDeleted,
                ThumbnailUrl = blog.ThumbnailUrl ?? string.Empty,
            }).ToList();

            return (blogDtos, totalCount);
        }

        public async Task<BlogDetailDto> GetSystemBlogByIdAsync(int systemBlogId)
        {
            var blog = await _unitOfWork.SystemBlogs.GetByIdAsync(systemBlogId);
            if (blog == null)
                throw new DomainException(BlogMessage.BlogError.BLOG_NOT_FOUND);
            return new BlogDetailDto
            {
                Title = blog.Title,
                ColorTitle = blog.ColorTitle,
                Content = blog.Content,
                ImageUrl = blog.ImageUrl,
                CreatedAt = blog.CreatedAt,
                UpdatedAt = blog.UpdatedAt,
                TotalViews = blog.TotalViews,
                BlogType = blog.BlogType,
                ThumbnailUrl = blog.ThumbnailUrl ?? string.Empty,
                IsDeleted = blog.IsDeleted == false,
                IsActive = blog.IsActive == true,
            };
        }
    }
}
