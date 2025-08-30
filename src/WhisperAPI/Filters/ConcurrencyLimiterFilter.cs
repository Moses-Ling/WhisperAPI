using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using WhisperAPI.Configuration;
using WhisperAPI.Services;

namespace WhisperAPI.Filters;

public sealed class ConcurrencyLimiterFilter : IAsyncActionFilter
{
    private readonly ConcurrencyLimiter _limiter;
    private readonly AppConfiguration _config;

    public ConcurrencyLimiterFilter(ConcurrencyLimiter limiter, IOptions<AppConfiguration> options)
    {
        _limiter = limiter;
        _config = options.Value;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(1, _config.Server.TimeoutSeconds));

        if (!await _limiter.TryEnterAsync(timeout, context.HttpContext.RequestAborted))
        {
            context.Result = new ObjectResult(new
            {
                error = new
                {
                    message = "Too many concurrent requests. Please try again later.",
                    type = "rate_limit_exceeded",
                    code = "concurrency_limit"
                }
            })
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };
            return;
        }

        try
        {
            await next();
        }
        finally
        {
            _limiter.Release();
        }
    }
}

