using ForlabsMcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// MCP over stdio talks JSON-RPC on stdout — all diagnostic logging must go to
// stderr instead, or it corrupts the protocol stream.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var baseUrl = Environment.GetEnvironmentVariable("FORLABS_BASE_URL") ?? "https://bki.forlabs.ru";
var username = Environment.GetEnvironmentVariable("FORLABS_USERNAME")
    ?? throw new InvalidOperationException("Set the FORLABS_USERNAME environment variable (your Forlabs login email).");
var password = Environment.GetEnvironmentVariable("FORLABS_PASSWORD")
    ?? throw new InvalidOperationException("Set the FORLABS_PASSWORD environment variable (your Forlabs password).");

var downloadDir = Environment.GetEnvironmentVariable("FORLABS_DOWNLOAD_DIR")
    ?? Path.Combine(Path.GetTempPath(), "forlabs-mcp", "downloads");

builder.Services.AddSingleton(new ForlabsClient(baseUrl, username, password));
builder.Services.AddSingleton<ForlabsApi>();
builder.Services.AddSingleton<ForlabsContext>();
builder.Services.AddSingleton(new DownloadOptions(downloadDir));

builder.Services
    .AddMcpServer(o =>
    {
        o.ServerInfo = new() { Name = "forlabs-mcp", Version = "1.0.0" };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();

public sealed record DownloadOptions(string Directory);
