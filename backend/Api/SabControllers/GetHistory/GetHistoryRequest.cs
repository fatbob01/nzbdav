using Microsoft.AspNetCore.Http;
using NzbWebDAV.Extensions;

namespace NzbWebDAV.Api.SabControllers.GetHistory;

public class GetHistoryRequest
{
    public int Start { get; init; } = 0;
    public int Limit { get; init; } = int.MaxValue;
    public string? Category { get; init; }
    public CancellationToken CancellationToken { get; set; }


    public GetHistoryRequest(HttpContext context)
    {
        var startParam = context.GetQueryParam("start");
        var limitParam = context.GetQueryParam("limit");
        Category = context.GetQueryParam("category");
        CancellationToken = context.RequestAborted;

        if (startParam is not null)
        {
            var isValidStartParam = int.TryParse(startParam, out int start);
            if (!isValidStartParam) throw new BadHttpRequestException("Invalid start parameter");
            Start = start;
        }

        if (limitParam is not null)
        {
            var isValidLimit = int.TryParse(limitParam, out int limit);
            if (!isValidLimit) throw new BadHttpRequestException("Invalid limit parameter");
            Limit = limit;
        }
    }
}