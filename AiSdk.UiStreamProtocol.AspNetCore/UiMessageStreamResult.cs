using System.Text.Json;
using AiSdk.UiStreamProtocol.Models;
using Microsoft.AspNetCore.Http;

namespace AiSdk.UiStreamProtocol.AspNetCore;

public class UiMessageStreamResult : IResult
{
    private readonly Func<Stream, Task> _writeStreamFunc;

    public UiMessageStreamResult(Func<Stream, Task> writeStreamFunc)
    {
        _writeStreamFunc = writeStreamFunc;
    }

    /// <summary>
    /// Creates a <see cref="UiMessageStreamResult"/> from an OpenAI-compatible streaming
    /// HTTP response (OpenRouter, Azure OpenAI, OpenAI, etc.). Reads the SSE chunks and
    /// translates them into the Vercel AI SDK UI stream protocol automatically.
    /// </summary>
    /// <param name="response">
    /// A streaming <see cref="HttpResponseMessage"/>. Send the upstream request with
    /// <see cref="HttpCompletionOption.ResponseHeadersRead"/> and call
    /// <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/> before passing it here.
    /// </param>
    public static UiMessageStreamResult FromOpenAiStream(HttpResponseMessage response)
    {
        return new UiMessageStreamResult(async stream =>
        {
            var writer = new UiSseWriter();
            bool textStarted = false;
            bool reasoningStarted = false;
            bool reasoningEnded = false;

            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (!line.StartsWith("data: ")) continue;
                var data = line["data: ".Length..];
                if (data == "[DONE]") break;

                JsonDocument doc;
                try { doc = JsonDocument.Parse(data); }
                catch (JsonException) { continue; }

                using (doc)
                {
                    var choices = doc.RootElement.GetProperty("choices");
                    if (choices.GetArrayLength() == 0) continue;
                    var delta = choices[0].GetProperty("delta");

                    // Reasoning content — supported by DeepSeek R1 and similar via OpenRouter
                    if (delta.TryGetProperty("reasoning", out var reasoning) && reasoning.GetString() is string reasoningText)
                    {
                        if (!reasoningStarted)
                        {
                            await writer.WriteAsync(stream, new ReasoningStart("reasoning-0"));
                            reasoningStarted = true;
                        }
                        await writer.WriteAsync(stream, new ReasoningDelta("reasoning-0", reasoningText));
                    }

                    // Text content — close reasoning block first if it was open
                    if (delta.TryGetProperty("content", out var content) && content.GetString() is string text)
                    {
                        if (reasoningStarted && !reasoningEnded)
                        {
                            await writer.WriteAsync(stream, new ReasoningEnd("reasoning-0"));
                            reasoningEnded = true;
                        }
                        if (!textStarted)
                        {
                            await writer.WriteAsync(stream, new TextStart("text-0"));
                            textStarted = true;
                        }
                        await writer.WriteAsync(stream, new TextDelta("text-0", text));
                    }
                }
            }

            if (reasoningStarted && !reasoningEnded)
                await writer.WriteAsync(stream, new ReasoningEnd("reasoning-0"));
            if (textStarted)
                await writer.WriteAsync(stream, new TextEnd("text-0"));

            await writer.WriteAsync(stream, new Finish());
            await writer.WriteAsync(stream, new Done());
        });
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache, no-transform";
        httpContext.Response.Headers["x-vercel-ai-ui-message-stream"] = "v1";
        httpContext.Response.Headers["Content-Encoding"] = "none";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        await _writeStreamFunc(httpContext.Response.Body);
    }
}
