using System.Text;
using System.Linq;
using NzbWebDAV.Config;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav.Base;
using Serilog;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreSymlinkFile(DavItem davFile, ConfigManager configManager) : BaseStoreReadonlyItem
{
    public override string Name => davFile.Name + ".rclonelink";
    public override string UniqueKey => davFile.Id + ".rclonelink";
    public override long FileSize => ContentBytes.Length;
    public override DateTime CreatedAt => davFile.CreatedAt;

    private byte[] ContentBytes => Encoding.UTF8.GetBytes(GetTargetPath());

    public override Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream>(new MemoryStream(ContentBytes));
    }

    private string GetTargetPath()
    {
        return GetTargetPath(davFile, configManager.GetRcloneMountDir());
    }

    public static string GetTargetPath(DavItem davFile, string mountDir)
    {
        var idSegments = davFile.IdPrefix
            .Select(x => x.ToString())
            .Prepend(DavItem.IdsFolder.Name)
            .Append(davFile.Id.ToString());

        // 1. Force explicit cleanup of the mount directory
        mountDir = NormalizeEscapedMountDir(mountDir);

        if (IsWindowsStylePath(mountDir))
        {
            // Rclone symlink targets are read as plain UTF-8 strings. Radarr/Sonarr expect
            // Windows-style absolute paths.
            var normalizedMount = NormalizeMountDirForWindowsTarget(mountDir);
            return JoinWithSeparator(normalizedMount, '\\', idSegments);
        }

        var pathParts = idSegments
            .Prepend(NormalizeMountDir(mountDir))
            .ToArray();

        return Path.Join(pathParts);
    }

    private static string NormalizeEscapedMountDir(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;

        // HARD FIX: Explicitly replace the known PUA characters.
        // The previous logic tried to guess these mathematically, but failed.
        // \uF03A =  (Weird Colon)
        // \uF05C =  (Weird Backslash)
        // \uF02F =  (Weird Slash)
        mountDir = mountDir
            .Replace('\uF03A', ':')
            .Replace('\uF05C', '\\')
            .Replace('\uF02F', '/');

        // Also normalize any other Private Use Area glyphs if they exist (fallback)
        mountDir = NormalizePrivateUseGlyphs(mountDir);

        return mountDir
            .Replace("\r\n", "\\r\\n")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    private static string NormalizePrivateUseGlyphs(string mountDir)
    {
        var builder = new StringBuilder(mountDir.Length);
        var substitutionsMade = false;

        foreach (var rune in mountDir.EnumerateRunes())
        {
            if (TryNormalizePrivateUseGlyph(rune, out var ascii))
            {
                builder.Append(ascii);
                substitutionsMade = true;
            }
            else
            {
                builder.Append(rune.ToString());
            }
        }

        var normalized = builder.ToString();
        if (substitutionsMade)
        {
            Log.Debug("Normalized private-use glyphs in mountDir from '{OriginalMountDir}' to '{NormalizedMountDir}'", mountDir, normalized);
        }

        return normalized;
    }

    private static bool TryNormalizePrivateUseGlyph(Rune rune, out char ascii)
    {
        ascii = default;
        var codePoint = rune.Value;

        var inPrivateUseRange = (codePoint >= 0xE000 && codePoint <= 0xF8FF)
                                 || (codePoint >= 0xF0000 && codePoint <= 0xFFFFD)
                                 || (codePoint >= 0x100000 && codePoint <= 0x10FFFD);

        if (!inPrivateUseRange) return false;

        foreach (var target in new[] { ':', '\\', '/' })
        {
            if (IsPrivateUseMatch(codePoint, target))
            {
                ascii = target;
                return true;
            }
        }

        return false;
    }

    private static bool IsPrivateUseMatch(int codePoint, char target)
    {
        if ((codePoint & 0xFF) == target)
        {
            return true;
        }

        var commonOffsets = new[]
        {
            0xE000, 0xF000, 0xF020, 0xF040, 0xF100,
            0xF0000, 0xF0020, 0xF0040, 0xF0100,
            0x100000, 0x100020, 0x100040, 0x100100
        };
        return commonOffsets.Any(offset => codePoint - target == offset);
    }

    private static string JoinWithSeparator(string mountDir, char separator, IEnumerable<string> idSegments)
    {
        // IMPORTANT: Trim PUA backslashes (\uF05C) as well as standard ones
        var trimmedMount = mountDir.TrimEnd('\\', '/', '\uF05C');
        
        var builder = new StringBuilder(trimmedMount);
        foreach (var segment in idSegments)
        {
            builder.Append(separator);
            builder.Append(segment);
        }

        return builder.ToString();
    }

    public static string NormalizeMountDir(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;

        var trimmed = mountDir.TrimEnd('\\', '/', '\uF05C');
        return IsWindowsStylePath(trimmed) ? trimmed.Replace('\\', '/').Replace('\uF05C', '/') : trimmed;
    }

    private static string NormalizeMountDirForWindowsTarget(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;

        var trimmed = mountDir.TrimEnd('\\', '/', '\uF05C');
        return trimmed.Replace('/', '\\').Replace('\uF05C', '\\');
    }

    public static string NormalizePathSeparators(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }

    private static bool IsWindowsStylePath(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return false;

        return mountDir.StartsWith("\\\\")
               || mountDir.StartsWith("//")
               || (mountDir.Length >= 2 && char.IsLetter(mountDir[0]) && (mountDir[1] == ':' || mountDir[1] == '\uF03A'));
    }
}
