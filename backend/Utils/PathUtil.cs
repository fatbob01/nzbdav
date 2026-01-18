namespace NzbWebDAV.Utils;

public class PathUtil
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static IEnumerable<string> GetAllParentDirectories(string path)
    {
        var directoryName = Path.GetDirectoryName(path);
        return !string.IsNullOrEmpty(directoryName)
            ? GetAllParentDirectories(directoryName).Prepend(directoryName)
            : [];
    }

    public static bool IsSubPath(string parent, string child)
    {
        var normalizedParent = NormalizePath(parent);
        var normalizedChild = NormalizePath(child);
        if (string.Equals(normalizedParent, normalizedChild, PathComparison))
            return true;

        var separator = Path.DirectorySeparatorChar;
        var parentWithSeparator = normalizedParent.EndsWith(separator)
            ? normalizedParent
            : normalizedParent + separator;
        return normalizedChild.StartsWith(parentWithSeparator, PathComparison);
    }

    public static bool AreSamePath(string pathOne, string pathTwo) =>
        string.Equals(NormalizePath(pathOne), NormalizePath(pathTwo), PathComparison);

    private static string NormalizePath(string path)
    {
        var normalized = Path.GetFullPath(path)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return normalized.TrimEnd(Path.DirectorySeparatorChar);
    }
}
