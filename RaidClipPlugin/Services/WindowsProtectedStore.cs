using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RaidClipPlugin.Services;

internal static class WindowsProtectedStore
{
    private static readonly byte[] Entropy = SHA256.HashData(
        Encoding.UTF8.GetBytes("RaidClipPlugin|WindowsUser|v1"));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static byte[] ProtectJson<T>(T value)
    {
        var clearData = JsonSerializer.SerializeToUtf8Bytes(
            value,
            JsonOptions);

        try
        {
            return ProtectedData.Protect(
                clearData,
                Entropy,
                DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearData);
        }
    }

    public static T? UnprotectJson<T>(byte[] protectedData)
    {
        var clearData = ProtectedData.Unprotect(
            protectedData,
            Entropy,
            DataProtectionScope.CurrentUser);

        try
        {
            return JsonSerializer.Deserialize<T>(clearData, JsonOptions);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearData);
        }
    }
}
