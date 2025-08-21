using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Api.SabControllers.AddFile;
using NzbWebDAV.Api.SabControllers.ClearQueue;
using NzbWebDAV.Api.SabControllers.GetConfig;
using NzbWebDAV.Api.SabControllers.GetFullStatus;
using NzbWebDAV.Api.SabControllers.GetHistory;
using NzbWebDAV.Api.SabControllers.GetQueue;
using NzbWebDAV.Api.SabControllers.GetVersion;
using NzbWebDAV.Api.SabControllers.RemoveFromHistory;
using NzbWebDAV.Api.SabControllers.RemoveFromQueue;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Extensions;
using NzbWebDAV.Services;
using NzbWebDAV.Utils;

namespace NzbWebDAV.Api.SabControllers;

[ApiController]
[Route("api")]
public class SabApiController(
    DavDatabaseClient dbClient,
    ConfigManager configManager,
    QueueManager queueManager
) : ControllerBase
{
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> HandleApiRequests()
    {
        try
        {
            var controller = GetController();
            return await controller.HandleRequest();
        }
        catch (BadHttpRequestException e)
        {
            return BadRequest(new SabBaseResponse()
            {
                Status = false,
                Error = e.Message
            });
        }
        catch (UnauthorizedAccessException e)
        {
            return Unauthorized(new SabBaseResponse()
            {
                Status = false,
                Error = e.Message
            });
        }
        catch (Exception e)
        {
            return StatusCode(500, new SabBaseResponse()
            {
                Status = false,
                Error = e.Message
            });
        }
    }

    public BaseController GetController()
    {
        var mode = HttpContext.GetQueryParam("mode");
        var name = HttpContext.GetQueryParam("name");

        return mode switch
        {
            "version" => new GetVersionController(HttpContext, configManager),
            "get_config" => new GetConfigController(HttpContext, configManager),
            "fullstatus" => new GetFullStatusController(HttpContext, configManager),
            "addfile" => new AddFileController(HttpContext, dbClient, queueManager, configManager),

            "queue" when name == "delete" =>
                new RemoveFromQueueController(HttpContext, dbClient, queueManager, configManager),
            "queue" when name == "delete_all" =>
                new ClearQueueController(HttpContext, dbClient, configManager, queueManager),
            "queue" => new GetQueueController(HttpContext, dbClient, queueManager, configManager),

            // del_completed_files handled within RemoveFromHistoryController
            "history" when name == "delete" =>
                new RemoveFromHistoryController(HttpContext, dbClient, configManager),
            "history" => new GetHistoryController(HttpContext, dbClient, configManager),

            _ => throw new BadHttpRequestException("Invalid mode"),
        };
    }

    public abstract class BaseController(HttpContext httpContext, ConfigManager configManager) : ControllerBase
    {
        public Task<IActionResult> HandleRequest()
        {
            if (RequiresAuthentication)
            {
                var apiKey = httpContext.GetRequestApiKey();
                var isValidKey = apiKey?.IsAny(
                    configManager.GetApiKey(),
                    EnvironmentUtil.GetVariable("FRONTEND_BACKEND_API_KEY")
                );
                if (!isValidKey.HasValue || !isValidKey.Value)
                    throw new UnauthorizedAccessException("API Key Required");
            }

            return Handle();
        }

        protected virtual bool RequiresAuthentication => true;
        protected abstract Task<IActionResult> Handle();
    }
}