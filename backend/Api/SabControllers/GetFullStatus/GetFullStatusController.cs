using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Config;
using NzbWebDAV.Database.Models;

namespace NzbWebDAV.Api.SabControllers.GetFullStatus;

public class GetFullStatusController(
    HttpContext httpContext,
    ConfigManager configManager
) : SabApiController.BaseController(httpContext, configManager)
{
    protected override Task<IActionResult> Handle()
    {
        // mimic sabnzbd fullstatus
        var completeDir = !string.IsNullOrWhiteSpace(configManager.GetSymlinkMirrorDir())
            ? Path.Join(configManager.GetRcloneMountDir(), "symlinks")
            : Path.Join(configManager.GetRcloneMountDir(), DavItem.SymlinkFolder.Name);
        var status = new GetFullStatusResponse()
        {
            Status = new GetFullStatusResponse.FullStatusObject()
            {
                CompleteDir = completeDir,
            }
        };

        return Task.FromResult<IActionResult>(Ok(status));
    }
}
