using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AiSdk.UiStreamProtocol.Models;

namespace AiSdk.UiStreamProtocol;

public class UiSseReader
{
    public async Task<UiStreamPart> ReadAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        string line;
        var sb = new System.Text.StringBuilder();
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (line.StartsWith("data:"))
            {
                sb.Append(line.Substring(5).Trim());
            }
            else if (line == "\n")
            {
                var json = sb.ToString();
                return JsonSerializer.Deserialize<UiStreamPart>(json);
            }
        }

        return null;
    }
}
