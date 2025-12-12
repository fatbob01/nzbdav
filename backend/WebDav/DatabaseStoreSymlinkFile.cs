using System.Text;
using System.Linq;
using System.Collections.Generic;
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

        mountDir = NormalizeEscapedMountDir(mountDir);

        if (IsWindowsStylePath(mountDir))
        {
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

        // --- DIAGNOSTIC LOGGING ---
        // This will print the exact unicode value of every character in the string.
        // We look for characters > 127 (non-ASCII).
        if (mountDir.Any(c => c > 127))
        {
            var hexDump = string.Join("-", mountDir.Select(c => $"{(int)c:X4}"));
            Log.Error(">>>>> CORRUPTION ANALYSIS <<<<<");
            Log.Error("Input String: {MountDir}", mountDir);
            Log.Error("Hex Dump:     {HexDump}", hexDump);
            Log.Error(">>>>> END ANALYSIS <<<<<");
        }
        // --------------------------

        // Standard Nerd Font replacements
        mountDir = mountDir
            .Replace('\uF03A', ':')
            .Replace('\uF05C', '\\')
            .Replace('\uF02F', '/');

        // Fallback to offset logic
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
        foreach (var rune in mountDir.EnumerateRunes())
        {
            if (TryNormalizePrivateUseGlyph(rune, out var ascii))
            {
                builder.Append(ascii);
            }
            else
            {
                builder.Append(rune.ToString());
            }
        }
        return builder.ToString();
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
        if ((codePoint & 0xFF) == target) return true;

        var commonOffsets = new[]
        {
            0xE000, 0xF000, 0xF020, 0xF040, 0xF100,
            0xF0000, 0xF0020, 0xF0040, 0xF0100,
            0x100000, 0x100020, 0x100040, 0x100100
        };
        return commonOffsets.Any(offset => codePoint - target == offset);
    }

    private static readonly char[] TrimSeparators = { '\\', '/', '', '' };

    private static string JoinWithSeparator(string mountDir, char separator, IEnumerable<string> idSegments)
    {
        // Important: Use the same PUA backslash if it wasn't normalized yet
        var builder = new StringBuilder(mountDir.TrimEnd(TrimSeparators));
        // Add specific PUA check for safety
        if (mountDir.Length > 0 && mountDir[mountDir.Length - 1] == '\uF05C')
        {
            builder.Length--; 
        }

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
        var trimmed = mountDir.TrimEnd(TrimSeparators);
        if (trimmed.EndsWith("\uF05C")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
        
        return IsWindowsStylePath(trimmed) ? trimmed.Replace('\\', '/') : trimmed;
    }

    private static string NormalizeMountDirForWindowsTarget(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return mountDir;
        var trimmed = mountDir.TrimEnd(TrimSeparators);
        if (trimmed.EndsWith("\uF05C")) trimmed = trimmed.Substring(0, trimmed.Length - 1);

        return trimmed.Replace('/', '\\');
    }

    public static string NormalizePathSeparators(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }

    private static bool IsWindowsStylePath(string mountDir)
    {
        if (string.IsNullOrEmpty(mountDir)) return false;
        
        // PUA colon check included
        return mountDir.StartsWith("\\\\")
               || mountDir.StartsWith("//")
               || (mountDir.Length >= 2 && char.IsLetter(mountDir[0]) && (mountDir[1] == ':' || mountDir[1] == '\uF03A'));
    }
}
