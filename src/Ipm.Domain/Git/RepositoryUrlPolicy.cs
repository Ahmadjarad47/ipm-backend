using System.Net;

namespace Ipm.Domain.Git;

/// <summary>R1: the server will call this address, so it must be a plain public https endpoint (SSRF guard, §7.2).</summary>
public static class RepositoryUrlPolicy
{
    /// <summary>An Arabic reason, or null when the URL is acceptable.</summary>
    public static string? Validate(string? raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var url))
        {
            return "رابط غير صالح";
        }

        if (url.Scheme != Uri.UriSchemeHttps)
        {
            return "يجب أن يبدأ بـ https://";
        }

        if (!string.IsNullOrEmpty(url.UserInfo))
        {
            return "لا تضع بيانات الدخول في الرابط";
        }

        if (!url.IsDefaultPort)
        {
            return "المنفذ غير مسموح (443 فقط)";
        }

        var host = url.IdnHost.ToLowerInvariant();
        if (host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal))
        {
            return "العنوان المحلي غير مسموح";
        }

        if (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IsPrivate(address))
        {
            return "عنوان شبكة خاصة غير مسموح";
        }

        return null;
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            return true; // could hide a private v4 address
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6Any) || (bytes[0] & 0xFE) == 0xFC || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80);
        }

        var b = address.GetAddressBytes();
        return b[0] is 10 or 127 or 0 || (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168);
    }
}
