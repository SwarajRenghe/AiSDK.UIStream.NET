using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AiSdk.UiStreamProtocol.Models;

namespace AiSdk.UiStreamProtocol;

public class UiSseWriter
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task WriteAsync(Stream stream, UiStreamPart part)
    {
        string data;
        if (part is Done)
        {
            data = "data: [DONE]\n\n";
        }
        else
        {
            var json = JsonSerializer.Serialize(part, part.GetType(), _options);
            data = $"data: {json}\n\n";
        }

        var bytes = Encoding.UTF8.GetBytes(data);
        await stream.WriteAsync(bytes, 0, bytes.Length);
    }
}
