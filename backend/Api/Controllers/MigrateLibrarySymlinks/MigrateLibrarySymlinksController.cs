using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Tasks;
using NzbWebDAV.Api.Controllers;

namespace NzbWebDAV.Api.Controllers.MigrateLibrarySymlinks;

[ApiController]
[Route("api/migrate-library-symlinks")]
public class MigrateLibrarySymlinksController(MigrateLibrarySymlinksTask task) : BaseApiController
{
    private readonly MigrateLibrarySymlinksTask _task = task;

    protected override async Task<IActionResult> HandleRequest()
    {
        await _task.ExecuteAsync(Array.Empty<string>());
        return Ok(new { status = true });
    }
}
