﻿using System.Text.Json.Serialization;
using NzbWebDAV.Database.Models;

namespace NzbWebDAV.Api.SabControllers.GetHistory;

public class GetHistoryResponse : SabBaseResponse
{
    [JsonPropertyName("history")]
    public HistoryObject History { get; set; }

    public class HistoryObject
    {
        [JsonPropertyName("slots")]
        public List<HistorySlot> Slots { get; set; }
    }

    public class HistorySlot
    {
        [JsonPropertyName("nzo_id")]
        public string NzoId { get; set; }

        [JsonPropertyName("nzb_name")]
        public string NzbName { get; set; }

        [JsonPropertyName("name")]
        public string JobName { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("status")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HistoryItem.DownloadStatusOption Status { get; set; }

        [JsonPropertyName("bytes")]
        public long SizeInBytes { get; set; }

        [JsonPropertyName("storage")]
        public string DownloadPath { get; set; }

        [JsonPropertyName("download_time")]
        public int DownloadTimeSeconds { get; set; }

        [JsonPropertyName("fail_message")]
        public string FailMessage { get; set; }
    }
}