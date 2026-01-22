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
        var symlinkMirrorDir = configManager.GetSymlinkMirrorDir();
        var completeDir = !string.IsNullOrWhiteSpace(symlinkMirrorDir)
            ? Path.Join(
                configManager.GetRcloneMountDir(),
                Path.GetFileName(symlinkMirrorDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            )
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
