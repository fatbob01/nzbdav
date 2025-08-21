using NzbWebDAV.Clients;
using NzbWebDAV.Extensions;
using Usenet.Nzb;

namespace NzbWebDAV.Services.FileProcessors;

public class FileProcessor(NzbFile nzbFile, UsenetProviderManager usenet, CancellationToken ct) : BaseProcessor
{
    public static bool CanProcess(NzbFile file)
    {
        // skip par2 files
        return !file.GetSubjectFileName().EndsWith(".par2", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<BaseProcessor.Result> ProcessAsync()
    {
        string headerFileName = "";
        if (nzbFile.Segments.Count > 0)
        {
            var firstSegment = nzbFile.Segments[0].MessageId.Value;
            var header = await usenet.GetSegmentYencHeaderAsync(firstSegment, ct);
            headerFileName = header.FileName;
        }

        var subjectFilename = nzbFile.GetSubjectFileName();
        var fileName = GetFileName(subjectFilename, headerFileName);
        var fileSize = await usenet.GetFileSizeAsync(nzbFile, ct);

        return new Result()
        {
            NzbFile = nzbFile,
            FileName = fileName,
            FileSize = fileSize,
        };
    }

    private string GetFileName(string subjectFilename, string headerFileName)
    {
        var subjectExtension = Path.GetExtension(subjectFilename);
        var headerExtension = Path.GetExtension(headerFileName);

        if (subjectExtension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) &&
            !headerExtension.Equals(".mkv", StringComparison.OrdinalIgnoreCase))
        {
            return subjectFilename;
        }

        var value = !string.IsNullOrEmpty(subjectFilename) ? subjectFilename
            : !string.IsNullOrEmpty(headerFileName) ? headerFileName
            : "unknown";
        return Path.GetFileName(value);
    }

    public new class Result : BaseProcessor.Result
    {
        public NzbFile NzbFile { get; init; } = null!;
        public string FileName { get; init; } = null!;
        public long FileSize { get; init; }
    }
}