using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ForlabsMcp;

/// <summary>
/// Thin HTTP client for the Forlabs LMS (Laravel + Angular "lm" stack).
/// Handles the Laravel/Sanctum-style cookie session + XSRF-TOKEN dance and
/// transparently re-authenticates on 401/419 responses.
/// </summary>
public sealed class ForlabsClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly CookieContainer _cookies = new();
    private readonly Uri _baseUri;
    private readonly string _username;
    private readonly string _password;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private bool _loggedIn;

    public ForlabsClient(string baseUrl, string username, string password)
    {
        _baseUri = new Uri(baseUrl.TrimEnd('/') + "/");
        _username = username;
        _password = password;

        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            UseCookies = true,
            AllowAutoRedirect = true,
        };

        _http = new HttpClient(handler) { BaseAddress = _baseUri };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ForlabsMcp/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        _http.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
    }

    private string? XsrfHeaderValue()
    {
        var cookie = _cookies.GetCookies(_baseUri)["XSRF-TOKEN"];
        if (cookie is null) return null;
        return Uri.UnescapeDataString(cookie.Value);
    }

    private async Task EnsureLoggedInAsync(CancellationToken ct)
    {
        if (_loggedIn) return;
        await _authLock.WaitAsync(ct);
        try
        {
            if (_loggedIn) return;
            await LoginAsync(ct);
            _loggedIn = true;
        }
        finally
        {
            _authLock.Release();
        }
    }

    private async Task LoginAsync(CancellationToken ct)
    {
        // Step 1: GET the login page to receive the XSRF-TOKEN + session cookies.
        using (var getReq = new HttpRequestMessage(HttpMethod.Get, "app/login"))
        {
            getReq.Headers.Referrer = _baseUri;
            using var getResp = await _http.SendAsync(getReq, ct);
            getResp.EnsureSuccessStatusCode();
        }

        var xsrf = XsrfHeaderValue()
            ?? throw new InvalidOperationException("Forlabs login: no XSRF-TOKEN cookie returned by the server.");

        // Step 2: POST credentials as JSON, echoing the XSRF token back as a header
        // (Laravel Sanctum "stateful" CSRF protection) plus Origin/Referer, which
        // the backend's stateful-domain check requires.
        var payload = JsonSerializer.Serialize(new
        {
            username = _username,
            password = _password,
            remember = true,
        });

        using var postReq = new HttpRequestMessage(HttpMethod.Post, "app/login")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        postReq.Headers.Add("X-XSRF-TOKEN", xsrf);
        postReq.Headers.Referrer = new Uri(_baseUri, "app/login");
        postReq.Headers.Add("Origin", _baseUri.GetLeftPart(UriPartial.Authority));

        using var postResp = await _http.SendAsync(postReq, ct);
        var body = await postResp.Content.ReadAsStringAsync(ct);

        if (!postResp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Forlabs login failed ({(int)postResp.StatusCode} {postResp.StatusCode}): {body}");
        }
    }

    /// <summary>
    /// Calls one of the Angular-app's JSON repository endpoints, e.g.
    /// "lm-vendor/repositories/learning/get_tasks", with an optional JSON body.
    /// Automatically logs in first, and retries once after a fresh login if the
    /// session turns out to be expired (401/419).
    /// </summary>
    public async Task<JsonNode?> PostJsonAsync(string relativePath, object? body, CancellationToken ct)
    {
        await EnsureLoggedInAsync(ct);
        var result = await SendJsonAsync(relativePath, body, ct);
        if (result.status is HttpStatusCode.Unauthorized or (HttpStatusCode)419)
        {
            // Session expired: force a fresh login and retry exactly once.
            _loggedIn = false;
            await EnsureLoggedInAsync(ct);
            result = await SendJsonAsync(relativePath, body, ct);
        }

        if ((int)result.status >= 400)
        {
            throw new InvalidOperationException(
                $"Forlabs API {relativePath} returned {(int)result.status} {result.status}: {result.body}");
        }

        if (string.IsNullOrWhiteSpace(result.body)) return null;
        return JsonNode.Parse(result.body);
    }

    private async Task<(HttpStatusCode status, string body)> SendJsonAsync(
        string relativePath, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, relativePath);
        var xsrf = XsrfHeaderValue();
        if (xsrf is not null) req.Headers.Add("X-XSRF-TOKEN", xsrf);
        req.Headers.Referrer = _baseUri;
        req.Headers.Add("Origin", _baseUri.GetLeftPart(UriPartial.Authority));

        var json = body is null ? "" : JsonSerializer.Serialize(body);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        return (resp.StatusCode, text);
    }

    /// <summary>GET a JSON endpoint under the app (e.g. "app/profile/user").</summary>
    public async Task<JsonNode?> GetJsonAsync(string relativePath, CancellationToken ct)
    {
        await EnsureLoggedInAsync(ct);
        var (status, body) = await SendGetAsync(relativePath, ct);
        if (status is HttpStatusCode.Unauthorized or (HttpStatusCode)419)
        {
            _loggedIn = false;
            await EnsureLoggedInAsync(ct);
            (status, body) = await SendGetAsync(relativePath, ct);
        }

        if ((int)status >= 400)
        {
            throw new InvalidOperationException($"Forlabs API {relativePath} returned {(int)status} {status}: {body}");
        }

        return string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body);
    }

    private async Task<(HttpStatusCode status, string body)> SendGetAsync(string relativePath, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, relativePath);
        req.Headers.Referrer = _baseUri;
        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        return (resp.StatusCode, text);
    }

    /// <summary>Downloads an arbitrary file URL (e.g. a task attachment) to disk.</summary>
    public async Task<(string path, long bytes, string? contentType)> DownloadFileAsync(
        string url, string destinationPath, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
        await using (var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
        await using (var stream = await resp.Content.ReadAsStreamAsync(ct))
        {
            await stream.CopyToAsync(fs, ct);
        }

        var info = new FileInfo(destinationPath);
        return (destinationPath, info.Length, resp.Content.Headers.ContentType?.MediaType);
    }

    public void Dispose() => _http.Dispose();
}
