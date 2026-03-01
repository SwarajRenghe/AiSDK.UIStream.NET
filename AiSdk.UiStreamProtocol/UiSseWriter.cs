using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiSdk.UiStreamProtocol.Models;

namespace AiSdk.UiStreamProtocol;

public class UiSseWriter
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task WriteAsync(
    Stream stream,
    UiStreamPart part,
    CancellationToken cancellationToken = default)
    {
        if (part is Done)
        {
            await stream.WriteAsync(
                Encoding.UTF8.GetBytes("data: [DONE]\n\n").AsMemory(),
                cancellationToken);
        }
        else
        {
            await stream.WriteAsync(
                Encoding.UTF8.GetBytes("data: ").AsMemory(),
                cancellationToken);

            await JsonSerializer.SerializeAsync(
                stream,
                part,
                part.GetType(),
                _options,
                cancellationToken);

            await stream.WriteAsync(
                Encoding.UTF8.GetBytes("\n\n").AsMemory(),
                cancellationToken);
        }

        await stream.FlushAsync(cancellationToken);
    }
}