using System.Reflection;

namespace NzbWebDAV.Extensions;

public static class ObjectExtensions
{
    public static object? GetReflectionProperty(this object obj, string propertyName)
    {
        var type = obj.GetType();
        var prop = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return prop?.GetValue(obj);
    }
}