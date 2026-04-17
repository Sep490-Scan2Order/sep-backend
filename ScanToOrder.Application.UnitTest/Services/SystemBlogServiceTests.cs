using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using ScanToOrder.Application.DTOs.SystemBlog;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class SystemBlogServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly Mock<ISystemBlogRepository> _mockRepo;
        private readonly SystemBlogService _service;

        public SystemBlogServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockStorageService = new Mock<IStorageService>();
            _mockRepo = new Mock<ISystemBlogRepository>();
            _mockUnitOfWork.Setup(u => u.SystemBlogs).Returns(_mockRepo.Object);
            _service = new SystemBlogService(_mockUnitOfWork.Object, _mockStorageService.Object);
        }

        #region AddSystemBlogAsync Tests

        [Fact]
        public async Task AddSystemBlogAsync_WithImagesAndThumbnail_ReturnsResponse()
        {
            // Arrange
            var mockImage = new Mock<IFormFile>();
            mockImage.Setup(f => f.Length).Returns(10);
            mockImage.Setup(f => f.FileName).Returns("image.jpg");

            var mockThumbnail = new Mock<IFormFile>();
            mockThumbnail.Setup(f => f.Length).Returns(20);
            mockThumbnail.Setup(f => f.FileName).Returns("thumb.jpg");

            var request = new AddSystemBlogDtoRequest
            {
                Title = "Title",
                Content = "Content",
                BlogType = default,
                Images = new List<IFormFile> { mockImage.Object },
                Thumbnail = mockThumbnail.Object
            };

            _mockStorageService.Setup(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), "s2o_blog"))
                .ReturnsAsync("http://image.url");
            _mockStorageService.Setup(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), "thumbnail"))
                .ReturnsAsync("http://thumb.url");

            // Act
            var result = await _service.AddSystemBlogAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.ImageUrl.Should().Contain("http://image.url");
            result.ThumbnailUrl.Should().Be("http://thumb.url");
            _mockUnitOfWork.Verify(u => u.SystemBlogs.AddAsync(It.IsAny<SystemBlog>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task AddSystemBlogAsync_NoImagesNoThumbnail_ReturnsResponse()
        {
            // Arrange
            var request = new AddSystemBlogDtoRequest
            {
                Title = "Title",
                Content = "Content",
                Images = null,
                Thumbnail = null
            };

            // Act
            var result = await _service.AddSystemBlogAsync(request);

            // Assert
            result.ImageUrl.Should().Be("[]");
            result.ThumbnailUrl.Should().BeEmpty();
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task AddSystemBlogAsync_EmptyFiles_SkipsUpload()
        {
            // Arrange
            var mockImage = new Mock<IFormFile>();
            mockImage.Setup(f => f.Length).Returns(0);
            var mockThumbnail = new Mock<IFormFile>();
            mockThumbnail.Setup(f => f.Length).Returns(0);

            var request = new AddSystemBlogDtoRequest
            {
                Images = new List<IFormFile> { mockImage.Object },
                Thumbnail = mockThumbnail.Object
            };

            // Act
            var result = await _service.AddSystemBlogAsync(request);

            // Assert
            result.ImageUrl.Should().Be("[]");
            _mockStorageService.Verify(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region GetSystemBlogsAsync Tests

        [Fact]
        public async Task GetSystemBlogsAsync_ReturnsItemsAndCount()
        {
            // Arrange
            var blogs = new List<SystemBlog>
            {
                new SystemBlog { SystemBlogId = 1, Title = "Blog 1", ThumbnailUrl = "thumb.jpg" },
                new SystemBlog { SystemBlogId = 2, Title = "Blog 2", ThumbnailUrl = null }
            };
            _mockUnitOfWork.Setup(u => u.SystemBlogs.GetSystemBlogsSortByCreatedDateAsync(1, 10, null))
                .ReturnsAsync((blogs, 2));

            // Act
            var result = await _service.GetSystemBlogsAsync(1, 10, null);

            // Assert
            result.Items.Should().HaveCount(2);
            result.TotalCount.Should().Be(2);
            result.Items[1].ThumbnailUrl.Should().BeEmpty();
        }

        #endregion

        #region GetSystemBlogByIdAsync Tests

        [Fact]
        public async Task GetSystemBlogByIdAsync_BlogExists_ReturnsDetail()
        {
            // Arrange
            var blog = new SystemBlog
            {
                SystemBlogId = 1,
                Title = "Title",
                ThumbnailUrl = "thumb.jpg",
                IsActive = true,
                IsDeleted = false
            };
            _mockUnitOfWork.Setup(u => u.SystemBlogs.GetByIdAsync(1)).ReturnsAsync(blog);

            // Act
            var result = await _service.GetSystemBlogByIdAsync(1);

            // Assert
            result.Title.Should().Be("Title");
            result.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task GetSystemBlogByIdAsync_BlogNotFound_ThrowsDomainException()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.SystemBlogs.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((SystemBlog)null);

            // Act
            Func<Task> act = () => _service.GetSystemBlogByIdAsync(1);

            // Assert
            await act.Should().ThrowAsync<DomainException>().WithMessage(BlogMessage.BlogError.BLOG_NOT_FOUND);
        }

        [Fact]
        public async Task GetSystemBlogByIdAsync_NullThumbnail_ReturnsEmptyStringInDto()
        {
            // Arrange
            var blog = new SystemBlog { SystemBlogId = 1, ThumbnailUrl = null };
            _mockUnitOfWork.Setup(u => u.SystemBlogs.GetByIdAsync(1)).ReturnsAsync(blog);

            // Act
            var result = await _service.GetSystemBlogByIdAsync(1);

            // Assert
            result.ThumbnailUrl.Should().BeEmpty();
        }

        [Fact]
        public async Task GetSystemBlogByIdAsync_CoversBooleanMappingBranches()
        {
            // Arrange
            var blog = new SystemBlog
            {
                SystemBlogId = 1,
                IsActive = false,
                IsDeleted = true
            };
            _mockUnitOfWork.Setup(u => u.SystemBlogs.GetByIdAsync(1)).ReturnsAsync(blog);

            // Act
            var result = await _service.GetSystemBlogByIdAsync(1);

            // Assert
            result.IsDeleted.Should().BeFalse();
            result.IsActive.Should().BeFalse();
        }
        #endregion
    }
}