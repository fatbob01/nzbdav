using Microsoft.AspNetCore.Http;
using NzbWebDAV.Extensions;

namespace NzbWebDAV.Api.Controllers.ListWebdavDirectory;

public class ListWebdavDirectoryRequest
{
    public string Directory { get; init; }

    public ListWebdavDirectoryRequest(HttpContext context)
    {
        Directory = context.Request.Form["directory"].FirstOrDefault() ?? "/";
    }
}