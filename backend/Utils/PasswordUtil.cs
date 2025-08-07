using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace NzbWebDAV.Utils;

public static class PasswordUtil
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions() { SizeLimit = 5 });

    public static string Hash(string password, string salt = "")
    {
        return Hasher.HashPassword(null!, password + salt);
    }

    public static bool Verify(string hash, string password, string salt = "")
    {
        // Optimize for cases when `--use-cookies` RClone flag is not used.
        // RClone makes a request every few seconds to check if the server is still alive.
        // Without the `--use-cookies` flag, RClone will send the same Basic Auth credentials
        // on every request, causing the server to hash the password repeatedly.
        // This cache helps reduce CPU usage by storing recent verification results.
        var cacheKey = new CacheKey(hash, password, salt);
        
        if (Cache.TryGetValue(cacheKey, out bool cachedResult))
        {
            return cachedResult;
        }

        var result = Hasher.VerifyHashedPassword(null!, hash, password + salt) == PasswordVerificationResult.Success;
        
        // Cache the result with a size of 1 and sliding expiration of 30 seconds
        Cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = TimeSpan.FromSeconds(30)
        });

        return result;
    }

    private record CacheKey(string Hash, string Password, string Salt);
}