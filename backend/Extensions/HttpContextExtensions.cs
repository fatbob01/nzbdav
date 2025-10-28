using Microsoft.AspNetCore.Http;

namespace NzbWebDAV.Extensions;

public static class HttpContextExtensions
{
    public static string? GetQueryParam(this HttpContext httpContext, string name)
    {
        return httpContext.Request.Query[name].FirstOrDefault();
    }

    public static string? GetRequestApiKey(this HttpContext httpContext)
    {
        return httpContext.Request.Headers["x-api-key"].FirstOrDefault()
            ?? httpContext.GetQueryParam("apikey");
    }
}