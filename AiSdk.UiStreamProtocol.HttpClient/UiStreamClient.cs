using System.Collections.Generic;
using AiSdk.UiStreamProtocol;
using AiSdk.UiStreamProtocol.Models;

namespace AiSdk.UiStreamProtocol.HttpClient;

public class UiStreamClient
{
    private readonly System.Net.Http.HttpClient _httpClient;

    public UiStreamClient(System.Net.Http.HttpClient httpClient)
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
