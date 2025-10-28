﻿using Microsoft.AspNetCore.StaticFiles;
using NWebDav.Server.Props;

namespace NzbWebDAV.WebDav.Base;

public class BaseStoreItemPropertyManager() : PropertyManager<BaseStoreItem>(DavProperties)
{
    private static readonly FileExtensionContentTypeProvider MimeTypeProvider = new();

    private static readonly DavProperty<BaseStoreItem>[] DavProperties =
    [
        new DavDisplayName<BaseStoreItem>
        {
            Getter = item => item.Name
        },
        new DavGetContentLength<BaseStoreItem>
        {
            Getter = item => item.FileSize
        },
        new DavGetContentType<BaseStoreItem>
        {
            Getter = item => !MimeTypeProvider.TryGetContentType(item.Name, out var mimeType)
                ? "application/octet-stream"
                : mimeType
        },
        new DavGetLastModified<BaseStoreItem>
        {
            Getter = x => x.CreatedAt
        },
        new Win32FileAttributes<BaseStoreItem>
        {
            Getter = _ => FileAttributes.Normal
        }
    ];

    public static readonly BaseStoreItemPropertyManager Instance = new();
}