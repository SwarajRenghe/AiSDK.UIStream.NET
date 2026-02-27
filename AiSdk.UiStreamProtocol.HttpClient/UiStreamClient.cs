using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

public class UiStreamClient
{
    private readonly HttpClient _httpClient;

    public UiStreamClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<UiStreamPart> GetStreamAsync(string url)
    {
        var response = await _httpClient.GetStreamAsync(url);

        var reader = new UiSseReader();
        while (true)
        {
            var part = await reader.ReadAsync(response);
            if (part == null)
                yield break;

            yield return part;
        }
    }
}
