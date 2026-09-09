using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Text;
using ECommerceBackend.API.Extensions;
using ECommerceBackend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

namespace ECommerceBackend.API.Filters
{
    [AttributeUsage(AttributeTargets.Method)]
    public class IdempotentAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var repository = serviceProvider.GetRequiredService<IIdempotencyRepository>();
            return new IdempotencyFilter(repository);
        }
    }

    public class IdempotencyFilter : IAsyncResourceFilter
    {
        private const string HeaderName = "Idempotency-Key";
        private const int MaximumKeyLength = 128;
        private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan LeaseRenewalInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CompletedResponseTtl = TimeSpan.FromHours(24);
        private static readonly HashSet<string> ReplayableHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Cache-Control",
            "Content-Disposition",
            "ETag",
            "Last-Modified",
            "Location",
            "Retry-After"
        };

        private readonly IIdempotencyRepository _repository;

        public IdempotencyFilter(IIdempotencyRepository repository)
        {
            _repository = repository;
        }

        public async Task OnResourceExecutionAsync(
            ResourceExecutingContext context,
            ResourceExecutionDelegate next)
        {
            if (!TryGetCallerKey(context.HttpContext.Request.Headers, out var callerKey))
            {
                context.Result = new BadRequestObjectResult(
                    $"{HeaderName} must contain one value between 1 and {MaximumKeyLength} ASCII characters using letters, numbers, '.', '_', ':', or '-'.");
                return;
            }

            var request = context.HttpContext.Request;
            var userId = context.HttpContext.User.GetRequiredUserId();
            var normalizedRoute = NormalizeRoute(request.Path);
            var storageKey = Hash(
                $"{userId:N}\n{request.Method.ToUpperInvariant()}\n{normalizedRoute}\n{callerKey}");
            var requestHash = await HashRequestBodyAsync(request, context.HttpContext.RequestAborted);
            var leaseId = Guid.NewGuid().ToString("N");
            var claim = await _repository.ClaimAsync(
                storageKey,
                requestHash,
                leaseId,
                ProcessingLease);

            var record = claim.Record
                ?? throw new InvalidOperationException("The idempotency claim has no record.");

            if (claim.Status == IdempotencyClaimStatus.Existing)
            {
                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(record.RequestHash),
                        Encoding.UTF8.GetBytes(requestHash)))
                {
                    context.Result = new ConflictObjectResult(
                        "This idempotency key was already used with different request data.");
                    return;
                }

                if (record.State == IdempotencyRecordState.Processing)
                {
                    context.Result = new ConflictObjectResult(
                        "This request is already being processed.");
                    return;
                }

                if (record.Response is null)
                    throw new InvalidOperationException("A completed idempotency record has no response.");

                context.Result = new IdempotencyReplayResult(record.Response);
                return;
            }

            await ExecuteAndRecordAsync(context, next, storageKey, record);
        }

        private async Task ExecuteAndRecordAsync(
            ResourceExecutingContext context,
            ResourceExecutionDelegate next,
            string storageKey,
            IdempotencyRecord processingRecord)
        {
            var response = context.HttpContext.Response;
            var originalBody = response.Body;
            await using var responseBuffer = new MemoryStream();
            response.Body = responseBuffer;
            using var renewalCancellation = new CancellationTokenSource();
            var renewalTask = RenewLeaseAsync(
                storageKey,
                processingRecord,
                renewalCancellation.Token);

            try
            {
                var executed = await next();
                renewalCancellation.Cancel();
                await AwaitLeaseRenewalAsync(renewalTask, renewalCancellation.Token);

                if (executed.Exception is not null && !executed.ExceptionHandled)
                {
                    await _repository.ReleaseAsync(storageKey, processingRecord);
                    return;
                }

                responseBuffer.Position = 0;
                var responseBody = responseBuffer.ToArray();

                if (response.StatusCode < StatusCodes.Status500InternalServerError)
                {
                    var storedResponse = new IdempotencyResponse(
                        response.StatusCode,
                        response.ContentType,
                        responseBody,
                        CaptureReplayableHeaders(response.Headers));
                    var completed = await _repository.CompleteAsync(
                        storageKey,
                        processingRecord,
                        storedResponse,
                        CompletedResponseTtl);

                    if (!completed)
                    {
                        throw new InvalidOperationException(
                            "The idempotency processing lease expired before the response could be recorded.");
                    }
                }
                else
                {
                    await _repository.ReleaseAsync(storageKey, processingRecord);
                }

                responseBuffer.Position = 0;
                await responseBuffer.CopyToAsync(
                    originalBody,
                    context.HttpContext.RequestAborted);
            }
            catch (Exception requestException)
            {
                renewalCancellation.Cancel();
                await _repository.ReleaseAsync(storageKey, processingRecord);
                try
                {
                    await AwaitLeaseRenewalAsync(renewalTask, renewalCancellation.Token);
                }
                catch (Exception renewalException)
                {
                    throw new AggregateException(requestException, renewalException);
                }

                ExceptionDispatchInfo.Capture(requestException).Throw();
                throw;
            }
            finally
            {
                response.Body = originalBody;
            }
        }

        private async Task RenewLeaseAsync(
            string storageKey,
            IdempotencyRecord processingRecord,
            CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(LeaseRenewalInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var renewed = await _repository.RenewAsync(
                    storageKey,
                    processingRecord,
                    ProcessingLease);
                if (!renewed)
                {
                    throw new InvalidOperationException(
                        "The idempotency processing lease could not be renewed.");
                }
            }
        }

        private static async Task AwaitLeaseRenewalAsync(
            Task renewalTask,
            CancellationToken cancellationToken)
        {
            try
            {
                await renewalTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private static bool TryGetCallerKey(IHeaderDictionary headers, out string callerKey)
        {
            callerKey = string.Empty;
            if (!headers.TryGetValue(HeaderName, out var values) || values.Count != 1)
                return false;

            callerKey = values[0]?.Trim() ?? string.Empty;
            return callerKey.Length is > 0 and <= MaximumKeyLength
                && callerKey.All(IsAllowedKeyCharacter);
        }

        private static bool IsAllowedKeyCharacter(char value) =>
            value is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '.'
                or '_'
                or ':'
                or '-';

        private static string NormalizeRoute(PathString path)
        {
            var normalized = path.Value?.TrimEnd('/').ToLowerInvariant();
            return string.IsNullOrEmpty(normalized) ? "/" : normalized;
        }

        private static async Task<string> HashRequestBodyAsync(
            HttpRequest request,
            CancellationToken cancellationToken)
        {
            request.EnableBuffering();
            request.Body.Position = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var representation = string.Join(
                '\n',
                request.ContentType?.Trim().ToLowerInvariant() ?? string.Empty,
                request.Headers.ContentEncoding.ToString().Trim().ToLowerInvariant());
            hash.AppendData(Encoding.UTF8.GetBytes(representation));
            hash.AppendData([0]);
            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await request.Body.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, bytesRead);
            }

            request.Body.Position = 0;
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        private static Dictionary<string, string[]> CaptureReplayableHeaders(IHeaderDictionary headers) =>
            headers
                .Where(header => ReplayableHeaders.Contains(header.Key))
                .ToDictionary(
                    header => header.Key,
                    header => header.Value.Select(value => value ?? string.Empty).ToArray(),
                    StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class IdempotencyReplayResult : IActionResult
    {
        private readonly IdempotencyResponse _response;

        public IdempotencyReplayResult(IdempotencyResponse response)
        {
            _response = response;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = _response.StatusCode;
            response.ContentType = _response.ContentType;

            foreach (var header in _response.Headers)
            {
                response.Headers[header.Key] = new StringValues(header.Value);
            }

            response.ContentLength = _response.Body.Length;
            await response.Body.WriteAsync(
                _response.Body,
                context.HttpContext.RequestAborted);
        }
    }
}
