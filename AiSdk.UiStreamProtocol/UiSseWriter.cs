using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AiSdk.UiStreamProtocol.Models;

namespace AiSdk.UiStreamProtocol;

public class UiSseWriter
{
    public async Task WriteAsync(Stream stream, UiStreamPart part)
    {
        var json = JsonSerializer.Serialize(part);
        var data = $"data: {json}\n\n";  // SSE frame
        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
        await stream.WriteAsync(bytes, 0, bytes.Length);
    }
}
