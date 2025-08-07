using System.Text.RegularExpressions;
using Usenet.Nzb;

namespace NzbWebDAV.Extensions;

public static class NzbFileExtensions
{
    public static string[] GetOrderedSegmentIds(this NzbFile file)
    {
        return file.Segments
            .OrderBy(x => x.Number)
            .Select(x => x.MessageId.Value)
            .ToArray();
    }

    public static string GetSubjectFileName(this NzbFile file)
    {
        return GetFirstValidNonEmptyFilename(
            () => TryParseFilename1(file),
            () => TryParseFilename2(file),
            () => TryParseFilename3(file),
            () => TryParseFilename4(file)
        );
    }

    private static string TryParseFilename1(this NzbFile file)
    {
        var match = Regex.Match(file.Subject, "\\\"(.*)\\\"");
        if (match.Success) return match.Groups[1].Value;
        return "";
    }

    private static string TryParseFilename2(this NzbFile file)
    {
        var matches = Regex.Matches(file.Subject, @"\[([^\[\]]*)\]");
        return matches
            .Select(x => x.Groups[1].Value)
            .Where(x => Path.GetExtension(x).StartsWith("."))
            .FirstOrDefault(x => Path.GetExtension(x).Length < 6) ?? "";
    }

    private static string TryParseFilename3(this NzbFile file)
    {
        var match = Regex.Match(file.Subject, @"^(.+\.(mkv|mp4|avi|wmv|flv|mov|m4v|webm|ts|m2ts|mts))\s+\(\d+/\d+\)");
        if (match.Success) return match.Groups[1].Value;
        return "";
    }

    private static string TryParseFilename4(this NzbFile file)
    {
        var match = Regex.Match(file.Subject, @"^(.+\.(mkv|mp4|avi|wmv|flv|mov|m4v|webm|ts|m2ts|mts))");
        if (match.Success) return match.Groups[1].Value;
        return "";
    }

    private static string GetFirstValidNonEmptyFilename(params Func<string>[] funcs)
    {
        return funcs
            .Select(x => x.Invoke())
            .FirstOrDefault(x => x != "" && IsValidFilename(x)) ?? "";
    }

    private static bool IsValidFilename(string filename)
    {
        return !string.IsNullOrWhiteSpace(filename) && 
               Path.GetExtension(filename).Length > 0 &&
               Path.GetExtension(filename).Length < 10 &&
               !Path.GetInvalidFileNameChars().Any(filename.Contains);
    }
}