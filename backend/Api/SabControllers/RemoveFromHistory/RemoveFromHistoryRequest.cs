using Microsoft.AspNetCore.Http;
using NzbWebDAV.Extensions;

namespace NzbWebDAV.Api.SabControllers.RemoveFromHistory;

public class RemoveFromHistoryRequest()
{
    public string NzoId { get; init; } = string.Empty;
    public bool DelCompletedFiles { get; init; }
    public CancellationToken CancellationToken { get; init; }

    public RemoveFromHistoryRequest(HttpContext httpContext): this()
    {
        NzoId = httpContext.GetQueryParam("value")!;
        var delParam = httpContext.GetQueryParam("del_completed_files");
        DelCompletedFiles = delParam == "1" || bool.TryParse(delParam, out var b) && b;
        CancellationToken = httpContext.RequestAborted;
    }
}