using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ECommerceBackend.Application.Exceptions;

namespace ECommerceBackend.API.Infrastructure
{
    /// <summary>
    /// Global exception handler that converts unhandled exceptions into RFC 7807 ProblemDetails responses.
    /// </summary>
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var (status, title, detail) = exception switch
            {
                ForbiddenAccessException => (
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    exception.Message),
                OrderStateConflictException => (
                    StatusCodes.Status409Conflict,
                    "Order state conflict",
                    exception.Message),
                KeyNotFoundException => (
                    StatusCodes.Status404NotFound,
                    "Resource not found",
                    exception.Message),
                ArgumentException => (
                    StatusCodes.Status400BadRequest,
                    "Invalid request",
                    exception.Message),
                UnauthorizedAccessException => (
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    exception.Message),
                _ => (
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred.",
                    "Please try again later. If the problem persists, contact support.")
            };

            if (status >= StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "Unhandled exception for {Path}", httpContext.Request.Path);
            else
                _logger.LogWarning(exception, "Request failed for {Path}", httpContext.Request.Path);

            var problemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };

            httpContext.Response.StatusCode = problemDetails.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
