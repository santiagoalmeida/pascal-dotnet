using System.Net.Http;
using System.Text;

namespace PascalRuntime;

// Outbound HTTP client — the counterpart to Http.cs (which serves requests, this one
// makes them). Works against any HTTP service: REST/JSON, SOAP (POST + the right
// Content-Type/SOAPAction headers and an XML envelope body), or anything else HTTP.
// Uses HttpClient.Send (the synchronous overload) since the language has no async/await.
public static class HttpReq
{
    private static readonly HttpClient Client = new();
    private static readonly Dictionary<string, string> PendingHeaders = new();
    private static string _contentType = "application/json";

    private static HttpResponseMessage? _response;
    private static string _responseBody = "";

    public static void SetHeader(string name, string value) => PendingHeaders[name] = value;

    public static void SetContentType(string contentType) => _contentType = contentType;

    private static HttpRequestMessage BuildRequest(HttpMethod method, string url, string? body)
    {
        var req = new HttpRequestMessage(method, url);
        foreach (var (name, value) in PendingHeaders)
            req.Headers.TryAddWithoutValidation(name, value);
        PendingHeaders.Clear();
        if (body is not null)
            req.Content = new StringContent(body, Encoding.UTF8, _contentType);
        return req;
    }

    private static bool Send(HttpRequestMessage req)
    {
        try
        {
            _response = Client.Send(req);
            using var stream = _response.Content.ReadAsStream();
            using var reader = new StreamReader(stream);
            _responseBody = reader.ReadToEnd();
            return _response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            _response = null;
            _responseBody = "";
            return false;
        }
    }

    public static bool Get(string url) => Send(BuildRequest(HttpMethod.Get, url, null));
    public static bool Post(string url, string body) => Send(BuildRequest(HttpMethod.Post, url, body));
    public static bool Put(string url, string body) => Send(BuildRequest(HttpMethod.Put, url, body));
    public static bool Delete(string url) => Send(BuildRequest(HttpMethod.Delete, url, null));

    public static int Status() => _response is null ? 0 : (int)_response.StatusCode;

    public static string Body() => _responseBody;

    public static string RespHeader(string name)
    {
        if (_response is null) return "";
        if (_response.Headers.TryGetValues(name, out var values)) return string.Join(",", values);
        if (_response.Content.Headers.TryGetValues(name, out var cValues)) return string.Join(",", cValues);
        return "";
    }
}
