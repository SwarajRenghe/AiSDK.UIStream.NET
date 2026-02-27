using System.Text;
using System.Text.Json;
using AiSdk.UiStreamProtocol;
using AiSdk.UiStreamProtocol.Models;
using Xunit;

/// <summary>
/// Verifies that UiSseWriter produces frames conforming to the Vercel AI SDK
/// UI Message Stream Protocol: https://ai-sdk.dev/docs/ai-sdk-ui/stream-protocol
/// </summary>
public class SseWriterTests
{
    private readonly UiSseWriter _writer = new();

    private async Task<string> WriteAsync(UiStreamPart part)
    {
        using var stream = new MemoryStream();
        await _writer.WriteAsync(stream, part);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static JsonElement ParseJson(string frame)
    {
        var json = frame["data: ".Length..].TrimEnd('\n');
        return JsonDocument.Parse(json).RootElement;
    }

    // --- Wire format ---

    [Fact]
    public async Task Frame_StartsWith_DataPrefix()
    {
        var frame = await WriteAsync(new TextDelta("id-1", "hello"));
        Assert.StartsWith("data: ", frame);
    }

    [Fact]
    public async Task Frame_EndsWith_DoubleNewline()
    {
        var frame = await WriteAsync(new TextDelta("id-1", "hello"));
        Assert.EndsWith("\n\n", frame);
    }

    [Fact]
    public async Task TypeField_IsLowerCamelCase()
    {
        var frame = await WriteAsync(new TextDelta("id-1", "hello"));
        var json = ParseJson(frame);
        Assert.True(json.TryGetProperty("type", out _), "Expected lowercase 'type' field");
        Assert.False(json.TryGetProperty("Type", out _), "Did not expect PascalCase 'Type' field");
    }

    // --- [DONE] terminator ---

    [Fact]
    public async Task Done_EmitsLiteral_NotJson()
    {
        // Spec: stream terminates with the literal line "data: [DONE]", not a JSON object
        var frame = await WriteAsync(new Done());
        Assert.Equal("data: [DONE]\n\n", frame);
    }

    // --- Text parts ---

    [Fact]
    public async Task TextStart_MatchesSpec()
    {
        // Spec: {"type":"text-start","id":"..."}
        var frame = await WriteAsync(new TextStart("text-1"));
        var json = ParseJson(frame);
        Assert.Equal("text-start", json.GetProperty("type").GetString());
        Assert.Equal("text-1", json.GetProperty("id").GetString());
    }

    [Fact]
    public async Task TextDelta_MatchesSpec()
    {
        // Spec: {"type":"text-delta","id":"...","delta":"..."}
        var frame = await WriteAsync(new TextDelta("text-1", "hello"));
        var json = ParseJson(frame);
        Assert.Equal("text-delta", json.GetProperty("type").GetString());
        Assert.Equal("text-1", json.GetProperty("id").GetString());
        Assert.Equal("hello", json.GetProperty("delta").GetString());
    }

    [Fact]
    public async Task TextEnd_MatchesSpec()
    {
        // Spec: {"type":"text-end","id":"..."}
        var frame = await WriteAsync(new TextEnd("text-1"));
        var json = ParseJson(frame);
        Assert.Equal("text-end", json.GetProperty("type").GetString());
        Assert.Equal("text-1", json.GetProperty("id").GetString());
    }

    // --- Reasoning parts ---

    [Fact]
    public async Task ReasoningDelta_MatchesSpec()
    {
        // Spec: {"type":"reasoning-delta","id":"...","delta":"..."}
        var frame = await WriteAsync(new ReasoningDelta("r-1", "thinking..."));
        var json = ParseJson(frame);
        Assert.Equal("reasoning-delta", json.GetProperty("type").GetString());
        Assert.Equal("r-1", json.GetProperty("id").GetString());
        Assert.Equal("thinking...", json.GetProperty("delta").GetString());
    }

    // --- Error ---

    [Fact]
    public async Task Error_MatchesSpec()
    {
        // Spec: {"type":"error","errorText":"..."}
        var frame = await WriteAsync(new Error("something failed"));
        var json = ParseJson(frame);
        Assert.Equal("error", json.GetProperty("type").GetString());
        Assert.Equal("something failed", json.GetProperty("errorText").GetString());
    }

    // --- Tool parts ---

    [Fact]
    public async Task ToolInputStart_MatchesSpec()
    {
        // Spec: {"type":"tool-input-start","toolCallId":"...","toolName":"..."}
        var frame = await WriteAsync(new ToolInputStart("call-1", "get_weather"));
        var json = ParseJson(frame);
        Assert.Equal("tool-input-start", json.GetProperty("type").GetString());
        Assert.Equal("call-1", json.GetProperty("toolCallId").GetString());
        Assert.Equal("get_weather", json.GetProperty("toolName").GetString());
    }

    [Fact]
    public async Task ToolInputAvailable_MatchesSpec()
    {
        // Spec: {"type":"tool-input-available","toolCallId":"...","toolName":"...","input":{...}}
        var frame = await WriteAsync(new ToolInputAvailable("call-1", "get_weather", new { city = "SF" }));
        var json = ParseJson(frame);
        Assert.Equal("tool-input-available", json.GetProperty("type").GetString());
        Assert.Equal("call-1", json.GetProperty("toolCallId").GetString());
        Assert.Equal("get_weather", json.GetProperty("toolName").GetString());
        Assert.True(json.TryGetProperty("input", out _), "Expected 'input' field");
    }

    [Fact]
    public async Task ToolOutputAvailable_MatchesSpec()
    {
        // Spec: {"type":"tool-output-available","toolCallId":"...","output":{...}}
        var frame = await WriteAsync(new ToolOutputAvailable("call-1", new { weather = "sunny" }));
        var json = ParseJson(frame);
        Assert.Equal("tool-output-available", json.GetProperty("type").GetString());
        Assert.Equal("call-1", json.GetProperty("toolCallId").GetString());
        Assert.True(json.TryGetProperty("output", out _), "Expected 'output' field");
    }

    // --- Control parts ---

    [Fact]
    public async Task Finish_MatchesSpec()
    {
        // Spec: {"type":"finish"}
        var frame = await WriteAsync(new Finish());
        var json = ParseJson(frame);
        Assert.Equal("finish", json.GetProperty("type").GetString());
    }
}
