using System.Net;
using System.Text;

namespace PascalRuntime;

// Minimal imperative HTTP server surface for the Pascal compiler's built-in HttpXxx
// functions/procedures. Deliberately stateful and single-request-at-a-time: it mirrors
// how a Pascal program without objects or closures can drive a request/response cycle.
public static class Http
{
    private static HttpListener? _listener;
    private static HttpListenerContext? _current;

    public static void Start(int port)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();
    }

    public static bool Wait()
    {
        if (_listener is null) return false;
        _current = _listener.GetContext();
        _cachedBody = null; // the request stream can only be read once; Body() may be called repeatedly
        return true;
    }

    public static string Method() => _current?.Request.HttpMethod ?? "";

    public static string Path() => _current?.Request.Url?.AbsolutePath ?? "";

    public static string Query(string key) => _current?.Request.QueryString[key] ?? "";

    public static string Header(string name) => _current?.Request.Headers[name] ?? "";

    public static string BearerToken()
    {
        const string prefix = "Bearer ";
        var auth = Header("Authorization");
        return auth.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? auth[prefix.Length..] : "";
    }

    private static Dictionary<string, string> _routeParams = new();

    // Matches the current request path against a pattern like "/users/:id" and, on a
    // match, captures ":id" segments for later retrieval via Param(). No wildcards/regex.
    public static bool Match(string pattern)
    {
        var pathParts = Path().Split('/', StringSplitOptions.RemoveEmptyEntries);
        var patternParts = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathParts.Length != patternParts.Length) return false;

        var captured = new Dictionary<string, string>();
        for (int i = 0; i < patternParts.Length; i++)
        {
            if (patternParts[i].StartsWith(':'))
                captured[patternParts[i][1..]] = pathParts[i];
            else if (!string.Equals(patternParts[i], pathParts[i], StringComparison.Ordinal))
                return false;
        }
        _routeParams = captured;
        return true;
    }

    public static string Param(string name) => _routeParams.TryGetValue(name, out var v) ? v : "";

    private static string? _cachedBody;

    public static string Body()
    {
        if (_cachedBody is not null) return _cachedBody;
        if (_current is null) return "";
        using var reader = new StreamReader(_current.Request.InputStream, _current.Request.ContentEncoding);
        return _cachedBody = reader.ReadToEnd();
    }

    public static void SetStatus(int code)
    {
        if (_current is not null) _current.Response.StatusCode = code;
    }

    public static void SetHeader(string name, string value)
    {
        _current?.Response.Headers.Add(name, value);
    }

    public static void Write(string text)
    {
        if (_current is null) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        _current.Response.OutputStream.Write(bytes, 0, bytes.Length);
    }

    public static void End()
    {
        if (_current is null) return;
        _current.Response.OutputStream.Close();
        _current = null;
    }
}
