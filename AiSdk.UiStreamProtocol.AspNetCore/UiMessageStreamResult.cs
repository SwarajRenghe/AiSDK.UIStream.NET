using System.Text;
using System.Text.Json;
using AiSdk.UiStreamProtocol.Models;
using Microsoft.AspNetCore.Http;

namespace AiSdk.UiStreamProtocol.AspNetCore;

/// <summary>
/// Represents a pending tool call extracted from an OpenAI-compatible streaming response.
/// </summary>
public sealed class ToolCall
{
    public string ToolCallId { get; init; } = "";
    public string Name { get; init; } = "";
    public JsonElement Input { get; init; }
}

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
            await ProcessOpenAiStream(response, stream, writer, cancellationToken);
            await writer.WriteAsync(stream, new Finish(), cancellationToken);
            await writer.WriteAsync(stream, new Done(), cancellationToken);
            await stream.FlushAsync(cancellationToken);
            response.Dispose();
        });
    }

    /// <summary>
    /// Creates a <see cref="UiMessageStreamResult"/> from an OpenAI-compatible streaming
    /// response, with support for tool calls and an agentic loop.
    /// 
    /// The library handles:
    /// - Streaming text and reasoning as it arrives
    /// - Detecting tool calls and emitting protocol events
    /// - Executing tools via your <paramref name="toolExecutor"/>
    /// - Calling <paramref name="continueConversation"/> with updated messages to continue the loop
    /// - Looping until the model returns finish_reason "stop"
    /// 
    /// You remain in control of all LLM communication — auth, model selection, HTTP, etc.
    /// </summary>
    /// <param name="response">The initial streaming response from your LLM provider.</param>
    /// <param name="toolExecutor">
    /// Called for each tool the model invokes. Return any serializable object as the result,
    /// or null if the tool is unknown.
    /// </param>
    /// <param name="continueConversation">
    /// Called after tool results are ready. Receives the full updated message history
    /// (original messages + assistant tool calls + tool results appended) so you can
    /// re-call your LLM and return the next streaming response.
    /// </param>
    public static UiMessageStreamResult FromOpenAiStreamWithTools(
        HttpResponseMessage response,
        IReadOnlyList<object> initialMessages,
        Func<ToolCall, Task<object?>> toolExecutor,
        Func<IReadOnlyList<object>, Task<HttpResponseMessage>> continueConversation)
    {
        return new UiMessageStreamResult(async (stream, cancellationToken) =>
        {
            var writer = new UiSseWriter();

            // Mutable message history we'll append to as the loop progresses
            var messages = new List<object>(initialMessages);
            var currentResponse = response;

            while (true)
            {
                var (toolCalls, finishReason) = await ProcessOpenAiStream(
                    currentResponse, stream, writer, cancellationToken);

                if (finishReason != "tool_calls" || toolCalls.Count == 0)
                    break;

                // --- Execute all tool calls and collect results ---

                // Append the assistant's tool-call turn to history
                var assistantToolCallMessage = new
                {
                    role = "assistant",
                    tool_calls = toolCalls.Select(tc => new
                    {
                        id = tc.ToolCallId,
                        type = "function",
                        function = new
                        {
                            name = tc.Name,
                            arguments = tc.Input.GetRawText()
                        }
                    }).ToArray()
                };
                messages.Add(assistantToolCallMessage);

                // Execute each tool and append its result
                foreach (var toolCall in toolCalls)
                {
                    await writer.WriteAsync(stream, new ToolInputAvailable(
                        toolCall.ToolCallId, toolCall.Name, toolCall.Input), cancellationToken);

                    var result = await toolExecutor(toolCall);
                    var resultJson = JsonSerializer.SerializeToElement(result ?? new { });

                    await writer.WriteAsync(stream, new ToolOutputAvailable(
                        toolCall.ToolCallId, resultJson), cancellationToken);

                    messages.Add(new
                    {
                        role = "tool",
                        tool_call_id = toolCall.ToolCallId,
                        content = JsonSerializer.Serialize(result ?? new { })
                    });
                }

                await stream.FlushAsync(cancellationToken);

                // --- Continue the conversation ---
                currentResponse = await continueConversation(messages.AsReadOnly());
                currentResponse.EnsureSuccessStatusCode();
            }

            await writer.WriteAsync(stream, new Finish(), cancellationToken);
            await writer.WriteAsync(stream, new Done(), cancellationToken);
            await stream.FlushAsync(cancellationToken);
            currentResponse.Dispose();
        });
    }

    /// <summary>
    /// Reads an OpenAI SSE stream, emitting AI SDK protocol events to <paramref name="outputStream"/>.
    /// Returns any tool calls found and the final finish_reason.
    /// </summary>
    private static async Task<(List<ToolCall> ToolCalls, string? FinishReason)> ProcessOpenAiStream(
        HttpResponseMessage response,
        Stream outputStream,
        UiSseWriter writer,
        CancellationToken cancellationToken)
    {
        bool textStarted = false;
        bool reasoningStarted = false;
        bool reasoningEnded = false;
        string? finishReason = null;

        // Accumulate tool call deltas — OpenAI streams these in fragments
        // keyed by index, e.g. index 0 = first tool call
        var toolCallBuilders = new Dictionary<int, ToolCallBuilder>();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            if (!line.StartsWith("data: ")) continue;

            var data = line.Substring(6);
            if (data == "[DONE]") break;

            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(data); }
            catch { continue; }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
                    choices.GetArrayLength() == 0)
                    continue;

                var choice = choices[0];

                if (choice.TryGetProperty("finish_reason", out var fr) &&
                    fr.ValueKind != JsonValueKind.Null)
                    finishReason = fr.GetString();

                var delta = choice.GetProperty("delta");

                // ---- REASONING ----
                if (delta.TryGetProperty("reasoning", out var reasoningProp))
                {
                    var reasoningText = reasoningProp.GetString();
                    if (!string.IsNullOrEmpty(reasoningText))
                    {
                        if (!reasoningStarted)
                        {
                            await writer.WriteAsync(outputStream, new ReasoningStart("reasoning-0"), cancellationToken);
                            reasoningStarted = true;
                        }
                        await writer.WriteAsync(outputStream, new ReasoningDelta("reasoning-0", reasoningText), cancellationToken);
                        await outputStream.FlushAsync(cancellationToken);
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
                            await writer.WriteAsync(outputStream, new ReasoningEnd("reasoning-0"), cancellationToken);
                            reasoningEnded = true;
                        }
                        if (!textStarted)
                        {
                            await writer.WriteAsync(outputStream, new TextStart("text-0"), cancellationToken);
                            textStarted = true;
                        }
                        await writer.WriteAsync(outputStream, new TextDelta("text-0", text), cancellationToken);
                        await outputStream.FlushAsync(cancellationToken);
                    }
                }

                // ---- TOOL CALLS ----
                if (delta.TryGetProperty("tool_calls", out var toolCallDeltas))
                {
                    foreach (var tcDelta in toolCallDeltas.EnumerateArray())
                    {
                        var index = tcDelta.GetProperty("index").GetInt32();

                        if (!toolCallBuilders.TryGetValue(index, out var builder))
                        {
                            builder = new ToolCallBuilder();
                            toolCallBuilders[index] = builder;
                        }

                        if (tcDelta.TryGetProperty("id", out var idProp))
                            builder.Id = idProp.GetString() ?? builder.Id;

                        if (tcDelta.TryGetProperty("function", out var funcProp))
                        {
                            if (funcProp.TryGetProperty("name", out var nameProp))
                            {
                                builder.Name = nameProp.GetString() ?? builder.Name;

                                // First chunk that has a name = start of this tool call
                                if (!builder.StartEmitted)
                                {
                                    await writer.WriteAsync(outputStream,
                                        new ToolInputStart(builder.Id, builder.Name), cancellationToken);
                                    builder.StartEmitted = true;
                                }
                            }

                            if (funcProp.TryGetProperty("arguments", out var argsProp))
                            {
                                var chunk = argsProp.GetString() ?? "";
                                builder.ArgumentsJson.Append(chunk);

                                if (!string.IsNullOrEmpty(chunk))
                                {
                                    await writer.WriteAsync(outputStream,
                                        new ToolInputDelta(builder.Id, chunk), cancellationToken);
                                    await outputStream.FlushAsync(cancellationToken);
                                }
                            }
                        }
                    }
                }
            }
        }

        // ---- CLEANUP ----
        if (reasoningStarted && !reasoningEnded)
            await writer.WriteAsync(outputStream, new ReasoningEnd("reasoning-0"), cancellationToken);

        if (textStarted)
            await writer.WriteAsync(outputStream, new TextEnd("text-0"), cancellationToken);

        // Materialise completed tool calls
        var toolCalls = toolCallBuilders.OrderBy(kv => kv.Key).Select(kv =>
        {
            var b = kv.Value;
            JsonElement input;
            try { input = JsonDocument.Parse(b.ArgumentsJson.ToString()).RootElement; }
            catch { input = JsonDocument.Parse("{}").RootElement; }
            return new ToolCall { ToolCallId = b.Id, Name = b.Name, Input = input };
        }).ToList();

        return (toolCalls, finishReason);
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

    private sealed class ToolCallBuilder
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public StringBuilder ArgumentsJson { get; } = new();
        public bool StartEmitted { get; set; }
    }
}