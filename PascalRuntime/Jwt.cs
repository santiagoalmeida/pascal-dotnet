using System.Security.Cryptography;
using System.Text;

namespace PascalRuntime;

// Minimal HS256 JWT (sign/verify/decode) using only BCL crypto — no external
// dependencies. Payload is an opaque JSON string; combine with Json.GetString/GetInt
// to read claims, and JwtNow() + an "exp" claim to check expiration.
public static class Jwt
{
    private const string HeaderJson = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";

    public static string Sign(string payloadJson, string secret)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes(HeaderJson));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = header + "." + payload;
        var signature = Base64UrlEncode(ComputeHmac(signingInput, secret));
        return signingInput + "." + signature;
    }

    public static bool Verify(string token, string secret)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return false;

        var signingInput = parts[0] + "." + parts[1];
        var expected = Base64UrlEncode(ComputeHmac(signingInput, secret));
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(parts[2]);
        if (expectedBytes.Length != actualBytes.Length) return false;
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public static string Payload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return "";
        try { return Encoding.UTF8.GetString(Base64UrlDecode(parts[1])); }
        catch (FormatException) { return ""; }
    }

    public static int Now() => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static byte[] ComputeHmac(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Convert.FromBase64String(s);
    }
}
