﻿using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Config;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Utils;

namespace NzbWebDAV.Api.SabControllers.GetConfig;

public class GetConfigController(
    HttpContext httpContext,
    ConfigManager configManager
) : SabApiController.BaseController(httpContext, configManager)
{
    /// <summary>
    /// Mimic the sabnzbd get_config api
    /// </summary>
    /// <returns>A valid sabnzbd config object</returns>
    protected override async Task<IActionResult> Handle()
    {
        // read the config template from file and deserialize it
        var config = await EmbeddedResourceUtil.ReadAllTextAsync("config_template.json");
        var root = JsonNode.Parse(config)!;

        // update the complete_dir
        var completeDir = Path.Join(configManager.GetRcloneMountDir(), DavItem.SymlinkFolder.Name);
        root["config"]!["misc"]!["complete_dir"] = completeDir;

        // update the categories
        var categoriesRoot = root["config"]?["categories"]?.AsArray()!;
        var categories = configManager.GetApiCategories().Split(',')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        foreach (var category in categories)
        {
            categoriesRoot.Add(new JsonObject
            {
                ["name"] = category,
                ["order"] = 0,
                ["pp"] = "",
                ["script"] = "Default",
                ["dir"] = category,
                ["newzbin"] = "",
                ["priority"] = -100
            });
        }

        // serialize the config object back to json
        var options = new JsonSerializerOptions { WriteIndented = true };
        var response = root.ToJsonString(options);
        return Content(response, "application/json");
    }
}