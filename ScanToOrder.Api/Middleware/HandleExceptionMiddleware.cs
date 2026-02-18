using Microsoft.EntityFrameworkCore;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace ScanToOrder.Api.Middleware
{
    public class HandleExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<HandleExceptionMiddleware> _logger;

        public HandleExceptionMiddleware(RequestDelegate next, ILogger<HandleExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var statusCode = (int)HttpStatusCode.InternalServerError;
            var message = "An unexpected error occurred on the server.";
            List<string>? errors = null;

            switch (exception)
            {
                case BaseException baseEx:
                    statusCode = baseEx.StatusCode;
                    message = baseEx.Message;
                    errors = baseEx.Errors;
                    break;

                case UnauthorizedAccessException:
                    statusCode = (int)HttpStatusCode.Unauthorized;
                    message = "Unauthorized access. Please provide a valid token.";
                    break;

                case KeyNotFoundException:
                    statusCode = (int)HttpStatusCode.NotFound;
                    message = "The requested resource was not found.";
                    break;

                case ArgumentException or InvalidOperationException:
                    statusCode = (int)HttpStatusCode.BadRequest;
                    message = exception.Message;
                    break;

                case DbUpdateException dbEx:
                    message = dbEx.InnerException?.Message ?? dbEx.Message;
                    errors = new List<string> { dbEx.ToString() };
                    break;

                default:
                    message = exception.InnerException?.Message ?? exception.Message;
                    errors = new List<string> { exception.StackTrace ?? "No stack trace available." };
                    break;
            }

            context.Response.StatusCode = statusCode;

            var response = ApiResponse<object>.Failure(message, errors);

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            return context.Response.WriteAsJsonAsync(response, options);
        }
    }
}
