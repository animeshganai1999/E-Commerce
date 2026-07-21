using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

namespace ECommerceBackend.API.Filters
{
    // Usage: [Idempotent] on a POST action.
    [AttributeUsage(AttributeTargets.Method)]
    public class IdempotentAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var repo = serviceProvider.GetRequiredService<IIdempotencyRepository>();
            return new IdempotencyFilter(repo);
        }
    }

    public class IdempotencyFilter : IAsyncActionFilter
    {
        private readonly IIdempotencyRepository _repo;
        private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
        private const string HeaderName = "Idempotency-Key";
        private const string InProgress = "in-progress";

        public IdempotencyFilter(IIdempotencyRepository repo) => _repo = repo;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 1. Require the header
            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues)
                || string.IsNullOrWhiteSpace(keyValues))
            {
                context.Result = new BadRequestObjectResult($"Missing {HeaderName} header.");
                return;
            }
            var key = keyValues.ToString();

            // 2. Try to claim
            bool claimed = await _repo.TryClaimAsync(key, Ttl);
            if (!claimed)
            {
                var stored = await _repo.GetAsync(key);
                if (stored == InProgress)
                {
                    context.Result = new ConflictObjectResult("This request is already being processed.");
                    return;
                }
                // Already completed → replay stored response
                context.Result = new ContentResult
                {
                    Content = stored,
                    ContentType = "application/json",
                    StatusCode = 200
                };
                return;
            }

            // 3. First time → run the action
            var executed = await next();

            // 4. Cache the response (or release claim on failure)
            if (executed.Exception == null && executed.Result is ObjectResult ok && IsSuccess(ok.StatusCode))
            {
                var json = JsonSerializer.Serialize(ok.Value);
                await _repo.SaveResponseAsync(key, json, Ttl);
            }
            else
            {
                // failed → let the user retry with the same key
                await _repo.RemoveAsync(key);
            }
        }

        private static bool IsSuccess(int? status) => status is null or (>= 200 and < 300);
    }
}