using System.Net;
using System.Text;
using System.Text.Json;
using AiSdk.UiStreamProtocol.AspNetCore;
using Microsoft.AspNetCore.Http;
using Xunit;

/// <summary>
/// Verifies that UiMessageStreamResult.FromOpenAiStream correctly translates
/// an OpenAI-compatible SSE response into the Vercel UI stream protocol.
/// </summary>
public class FromOpenAiStreamTests
{
    // --- Helpers ---

    private static string ContentChunk(string text) =>
        $"data: {{\"choices\":[{{\"delta\":{{\"content\":\"{text}\"}}}}]}}";

    private static string ReasoningChunk(string text) =>
        $"data: {{\"choices\":[{{\"delta\":{{\"reasoning\":\"{text}\"}}}}]}}";

    private const string DoneChunk = "data: [DONE]";

    private static HttpResponseMessage MakeResponse(params string[] lines)
    {
        var body = string.Join("\n\n", lines) + "\n\n";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
        };
    }

    private static async Task<string[]> GetFramesAsync(HttpResponseMessage response)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await UiMessageStreamResult.FromOpenAiStream(response).ExecuteAsync(context);

        context.Response.Body.Position = 0;
        var output = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return output
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .Where(f => f.StartsWith("data: "))
            .ToArray();
    }

    private static JsonElement Json(string frame) =>
        JsonDocument.Parse(frame["data: ".Length..]).RootElement;

    // --- Tests ---

    [Fact]
    public async Task SingleChunk_EmitsCorrectSequence()
    {
        // TextStart → TextDelta → TextEnd → Finish → [DONE]
        var frames = await GetFramesAsync(MakeResponse(ContentChunk("Hello!"), DoneChunk));

        Assert.Equal(5, frames.Length);
        Assert.Equal("text-start",  Json(frames[0]).GetProperty("type").GetString());
        Assert.Equal("text-delta",  Json(frames[1]).GetProperty("type").GetString());
        Assert.Equal("Hello!",      Json(frames[1]).GetProperty("delta").GetString());
        Assert.Equal("text-end",    Json(frames[2]).GetProperty("type").GetString());
        Assert.Equal("finish",      Json(frames[3]).GetProperty("type").GetString());
        Assert.Equal("data: [DONE]", frames[4]);
    }

    [Fact]
    public async Task MultipleChunks_EmitsOneTextStartAndOneTextEnd()
    {
        // Only one TextStart and one TextEnd regardless of how many deltas arrive
        var frames = await GetFramesAsync(MakeResponse(
            ContentChunk("Hello"),
            ContentChunk(" "),
            ContentChunk("world!"),
            DoneChunk));

        Assert.Equal("text-start", Json(frames[0]).GetProperty("type").GetString());
        Assert.Equal("text-delta", Json(frames[1]).GetProperty("type").GetString());
        Assert.Equal("Hello",      Json(frames[1]).GetProperty("delta").GetString());
        Assert.Equal("text-delta", Json(frames[2]).GetProperty("type").GetString());
        Assert.Equal(" ",          Json(frames[2]).GetProperty("delta").GetString());
        Assert.Equal("text-delta", Json(frames[3]).GetProperty("type").GetString());
        Assert.Equal("world!",     Json(frames[3]).GetProperty("delta").GetString());
        Assert.Equal("text-end",   Json(frames[4]).GetProperty("type").GetString());
    }

    [Fact]
    public async Task TextStart_TextDelta_TextEnd_ShareTheSameId()
    {
        var frames = await GetFramesAsync(MakeResponse(ContentChunk("Hi"), DoneChunk));

        var startId = Json(frames[0]).GetProperty("id").GetString();
        var deltaId = Json(frames[1]).GetProperty("id").GetString();
        var endId   = Json(frames[2]).GetProperty("id").GetString();

        Assert.Equal(startId, deltaId);
        Assert.Equal(startId, endId);
    }

    [Fact]
    public async Task EmptyStream_EmitsOnlyFinishAndDone()
    {
        // No content chunks → no text parts at all
        var frames = await GetFramesAsync(MakeResponse(DoneChunk));

        Assert.Equal(2, frames.Length);
        Assert.Equal("finish",       Json(frames[0]).GetProperty("type").GetString());
        Assert.Equal("data: [DONE]", frames[1]);
    }

    [Fact]
    public async Task ReasoningChunks_EmitReasoningPartsBeforeText()
    {
        // ReasoningStart → ReasoningDelta → ReasoningEnd → TextStart → TextDelta → TextEnd → Finish → [DONE]
        var frames = await GetFramesAsync(MakeResponse(
            ReasoningChunk("thinking..."),
            ContentChunk("Answer"),
            DoneChunk));

        Assert.Equal(8, frames.Length);
        Assert.Equal("reasoning-start", Json(frames[0]).GetProperty("type").GetString());
        Assert.Equal("reasoning-delta", Json(frames[1]).GetProperty("type").GetString());
        Assert.Equal("thinking...",     Json(frames[1]).GetProperty("delta").GetString());
        Assert.Equal("reasoning-end",   Json(frames[2]).GetProperty("type").GetString());
        Assert.Equal("text-start",      Json(frames[3]).GetProperty("type").GetString());
        Assert.Equal("text-delta",      Json(frames[4]).GetProperty("type").GetString());
        Assert.Equal("Answer",          Json(frames[4]).GetProperty("delta").GetString());
        Assert.Equal("text-end",        Json(frames[5]).GetProperty("type").GetString());
    }

    [Fact]
    public async Task ReasoningOnly_EmitsReasoningPartsWithNoTextParts()
    {
        // reasoning-start, reasoning-delta, reasoning-end, finish, [DONE] = 5 frames
        var frames = await GetFramesAsync(MakeResponse(ReasoningChunk("hmm"), DoneChunk));

        Assert.Equal(5, frames.Length);
        Assert.Equal("reasoning-start", Json(frames[0]).GetProperty("type").GetString());
        Assert.Equal("reasoning-delta", Json(frames[1]).GetProperty("type").GetString());
        Assert.Equal("reasoning-end",   Json(frames[2]).GetProperty("type").GetString());
        Assert.Equal("finish",          Json(frames[3]).GetProperty("type").GetString());
        Assert.Equal("data: [DONE]",    frames[4]);
    }

    [Fact]
    public async Task EmptyContentChunks_AreNotEmittedAsDeltaFrames()
    {
        // OpenAI-compatible APIs often send empty-string content on the first/last chunks
        var frames = await GetFramesAsync(MakeResponse(
            ContentChunk(""),
            ContentChunk("Hello"),
            ContentChunk(""),
            DoneChunk));

        var deltas = frames.Where(f => f.Contains("\"text-delta\"")).ToArray();
        Assert.Single(deltas);
        Assert.Equal("Hello", Json(deltas[0]).GetProperty("delta").GetString());
    }

    [Fact]
    public async Task MalformedJsonLines_AreSkippedGracefully()
    {
        // A bad SSE line in the middle should not crash the stream
        var frames = await GetFramesAsync(MakeResponse(
            "data: not valid json{{{{",
            ContentChunk("Hi"),
            DoneChunk));

        Assert.Contains(frames, f => f.Contains("\"text-delta\""));
    }

    [Fact]
    public async Task NonDataLines_AreIgnored()
    {
        // SSE comment lines and blank lines should be ignored
        var body = ": keep-alive\n\n" + ContentChunk("Hi") + "\n\n" + DoneChunk + "\n\n";
        var frames = await GetFramesAsync(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
        });

        Assert.Contains(frames, f => f.Contains("\"text-delta\""));
    }
}
