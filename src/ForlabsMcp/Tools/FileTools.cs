using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ForlabsMcp.Tools;

[McpServerToolType]
public sealed class FileTools(ForlabsClient client, DownloadOptions downloadOptions)
{
    [McpServerTool(Name = "forlabs_download_task_file"),
     Description("Downloads a file attached to a homework task (or a chat message) to local disk, so it can " +
                  "be read/used directly — e.g. by Claude Code or Codex working on the assignment. Pass the " +
                  "'url' from forlabs_get_homework_details / forlabs_get_task_chat's file entries.")]
    public async Task<string> DownloadTaskFile(
        [Description("The direct file URL, as returned in a task/comment's 'files[].url' field.")] string url,
        [Description("Optional filename to save as. Defaults to the name in the URL.")] string? filename,
        CancellationToken ct)
    {
        var name = filename ?? Path.GetFileName(new Uri(url).LocalPath);
        if (string.IsNullOrWhiteSpace(name)) name = "download.bin";
        var dest = Path.Combine(downloadOptions.Directory, name);

        var (path, bytes, contentType) = await client.DownloadFileAsync(url, dest, ct);
        return JsonUtil.Pretty(new { saved_to = Path.GetFullPath(path), bytes, content_type = contentType });
    }
}
