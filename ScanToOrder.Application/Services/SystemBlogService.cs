using ScanToOrder.Application.DTOs.SystemBlog;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class SystemBlogService : ISystemBlogService
    {
        private readonly IUnitOfWork _unitOfWork;
        public SystemBlogService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<AddSystemBlogDtoResponse> AddSystemBlogAsync(AddSystemBlogDtoRequest request)
        {
            var systemBlog = new SystemBlog
            {
                Content = request.Content,
                Title = request.Title,
                ColorTitle = request.ColorTitle,
                ImageUrl = request.ImageUrl,
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
                CreatedAt = systemBlog.CreatedAt,
                UpdatedAt = systemBlog.UpdatedAt,
                TotalViews = systemBlog.TotalViews,
                ImageUrl = systemBlog.ImageUrl,
                BlogType = systemBlog.BlogType
            };
        }

        public async Task<IEnumerable<SystemBlog>> GetSystemBlogAsync()
        {
             return await _unitOfWork.SystemBlogs.GetAllAsync();
        }
    }
}
