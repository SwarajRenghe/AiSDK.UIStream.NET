using System.Text.Json;
using AiSdk.UiStreamProtocol.Models;
using Microsoft.AspNetCore.Http;

namespace AiSdk.UiStreamProtocol.AspNetCore;

public class UiMessageStreamResult : IResult
{
    private readonly Func<Stream, CancellationToken, Task> _writeStreamFunc;

    public UiMessageStreamResult(Func<Stream, CancellationToken, Task> writeStreamFunc)
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
        return new UiMessageStreamResult(async (stream, cancellationToken) =>
        {
            var writer = new UiSseWriter();

            bool textStarted = false;
            bool reasoningStarted = false;
            bool reasoningEnded = false;

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(responseStream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                if (!line.StartsWith("data: "))
                    continue;

                var data = line.Substring(6);

                if (data == "[DONE]")
                    break;

                JsonDocument? doc = null;
                try
                {
                    doc = JsonDocument.Parse(data);
                }
                catch
                {
                    continue;
                }

                using (doc)
                {
                    if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
                        choices.GetArrayLength() == 0)
                        continue;

                    var delta = choices[0].GetProperty("delta");

                    // ---- REASONING ----
                    if (delta.TryGetProperty("reasoning", out var reasoningProp))
                    {
                        var reasoningText = reasoningProp.GetString();
                        if (!string.IsNullOrEmpty(reasoningText))
                        {
                            if (!reasoningStarted)
                            {
                                await writer.WriteAsync(stream, new ReasoningStart("reasoning-0"), cancellationToken);
                                reasoningStarted = true;
                            }

                            await writer.WriteAsync(stream, new ReasoningDelta("reasoning-0", reasoningText), cancellationToken);
                            await stream.FlushAsync(cancellationToken);
                        }
                    }

                    // ---- TEXT ----
                    if (delta.TryGetProperty("content", out var contentProp))
                    {
                        var text = contentProp.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            if (reasoningStarted && !reasoningEnded)
                            {
                                await writer.WriteAsync(stream, new ReasoningEnd("reasoning-0"), cancellationToken);
                                reasoningEnded = true;
                            }

                            if (!textStarted)
                            {
                                await writer.WriteAsync(stream, new TextStart("text-0"), cancellationToken);
                                textStarted = true;
                            }

                            await writer.WriteAsync(stream, new TextDelta("text-0", text), cancellationToken);
                            await stream.FlushAsync(cancellationToken);
                        }
                    }
                }
            }

            // ---- CLEANUP ----

            if (reasoningStarted && !reasoningEnded)
            {
                await writer.WriteAsync(stream, new ReasoningEnd("reasoning-0"), cancellationToken);
            }

            if (textStarted)
            {
                await writer.WriteAsync(stream, new TextEnd("text-0"), cancellationToken);
            }

            await writer.WriteAsync(stream, new Finish(), cancellationToken);
            await writer.WriteAsync(stream, new Done(), cancellationToken);

            response.Dispose();
            await stream.FlushAsync(cancellationToken);
        });
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache, no-transform";
        httpContext.Response.Headers["x-vercel-ai-ui-message-stream"] = "v1";
        httpContext.Response.Headers["Content-Encoding"] = "none";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        await _writeStreamFunc(httpContext.Response.Body, httpContext.RequestAborted);
    }
}
