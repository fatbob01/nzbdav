using System.Text.RegularExpressions;

namespace NzbWebDAV.Utils;

public static class ObfuscationUtil
{
    public static bool IsProbablyObfuscated(string filename)
    {
        var name = Path.GetFileName(filename);
        var baseName = Path.GetFileNameWithoutExtension(name);

        if (string.IsNullOrEmpty(baseName))
            return true;

        if (Regex.IsMatch(baseName, "^[a-f0-9]{32}$", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(baseName, "^[a-f0-9.]{40,}$", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(baseName, "[a-f0-9]{30}", RegexOptions.IgnoreCase) &&
            Regex.Matches(baseName, @"\[\w+\]").Count >= 2)
            return true;

        if (Regex.IsMatch(baseName, "^abc\\.xyz", RegexOptions.IgnoreCase))
            return true;

        int decimals = 0;
        int upperChars = 0;
        int lowerChars = 0;
        int spacesDots = 0;

        foreach (var c in baseName)
        {
            if (char.IsDigit(c))
                decimals++;
            else if (char.IsUpper(c))
                upperChars++;
            else if (char.IsLower(c))
                lowerChars++;

            if (c == ' ' || c == '.' || c == '_')
                spacesDots++;
        }

        if (upperChars >= 2 && lowerChars >= 2 && spacesDots >= 1)
            return false;

        if (spacesDots >= 3)
            return false;

        if ((upperChars + lowerChars >= 4) && decimals >= 4 && spacesDots >= 1)
            return false;

        if (char.IsUpper(baseName[0]) && lowerChars > 2 && lowerChars > 0 &&
            (double)upperChars / lowerChars <= 0.25)
            return false;

        return true;
    }
}
