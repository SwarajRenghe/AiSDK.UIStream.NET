# AiSdk.UiStreamProtocol

[![CI](https://github.com/SwarajRenghe/Dotnet-Vercel-UIStream-Protocol/actions/workflows/ci.yml/badge.svg)](https://github.com/SwarajRenghe/Dotnet-Vercel-UIStream-Protocol/actions/workflows/ci.yml)

A .NET implementation of the [Vercel AI SDK UI Stream Protocol](https://sdk.vercel.ai/docs/ai-sdk-ui/stream-protocol). It handles the SSE wire format — typed stream parts like `text-delta`, `tool-input-start`, `finish` — that `useChat` on the frontend knows how to consume.

## Packages

| Package | NuGet | Description |
|---|---|---|
| `AiSdk.UiStreamProtocol` | [![NuGet](https://img.shields.io/nuget/v/AiSdk.UiStreamProtocol)](https://www.nuget.org/packages/AiSdk.UiStreamProtocol/) | Core types, SSE reader/writer, message assembler |
| `AiSdk.UiStreamProtocol.AspNetCore` | [![NuGet](https://img.shields.io/nuget/v/AiSdk.UiStreamProtocol.AspNetCore)](https://www.nuget.org/packages/AiSdk.UiStreamProtocol.AspNetCore/) | Serve a UI stream from an ASP.NET Core endpoint |
| `AiSdk.UiStreamProtocol.HttpClient` | [![NuGet](https://img.shields.io/nuget/v/AiSdk.UiStreamProtocol.HttpClient)](https://www.nuget.org/packages/AiSdk.UiStreamProtocol.HttpClient/) | Consume a UI stream from another service as `IAsyncEnumerable` |

## Usage

### Bridging an AI provider to the frontend (OpenRouter example)

The most common pattern: your ASP.NET Core endpoint calls an AI provider, translates the response into the UI stream protocol, and pipes it to `useChat` on the frontend.

```sh
dotnet add package AiSdk.UiStreamProtocol.AspNetCore
```

```csharp
using System.Text;
using System.Text.Json;
using AiSdk.UiStreamProtocol;
using AiSdk.UiStreamProtocol.AspNetCore;
using AiSdk.UiStreamProtocol.Models;

app.MapPost("/api/chat", (IConfiguration config) =>
{
    return new UiMessageStreamResult(async stream =>
    {
        var writer = new UiSseWriter();

        // Call OpenRouter (OpenAI-compatible streaming API)
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {config["OpenRouter:ApiKey"]}");

        var body = JsonSerializer.Serialize(new
        {
            model = "anthropic/claude-3.5-sonnet",
            stream = true,
            messages = new[] { new { role = "user", content = "Hello!" } }
        });

        using var response = await http.PostAsync(
            "https://openrouter.ai/api/v1/chat/completions",
            new StringContent(body, Encoding.UTF8, "application/json"));

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());

        await writer.WriteAsync(stream, new TextStart("text-1"));

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line is null || !line.StartsWith("data: ")) continue;
            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            var doc = JsonDocument.Parse(data);
            var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
            if (delta.TryGetProperty("content", out var content) && content.GetString() is string text)
                await writer.WriteAsync(stream, new TextDelta("text-1", text));
        }

        await writer.WriteAsync(stream, new TextEnd("text-1"));
        await writer.WriteAsync(stream, new Finish());
        await writer.WriteAsync(stream, new Done());
    });
});
```

On the React side, point `useChat` at your endpoint:

```tsx
import { useChat } from '@ai-sdk/react';
import { TextStreamChatTransport } from 'ai';

const { messages, sendMessage } = useChat({
  transport: new TextStreamChatTransport({ api: '/api/chat' }),
});
```

`UiMessageStreamResult` sets all required headers (`Content-Type: text/event-stream`, `x-vercel-ai-ui-message-stream: v1`, etc.) automatically.

---

### Consuming a stream between .NET services

If you have a service (or microservice) that already produces a UI stream, read it as an `IAsyncEnumerable` of typed parts:

```sh
dotnet add package AiSdk.UiStreamProtocol.HttpClient
```

```csharp
using AiSdk.UiStreamProtocol.HttpClient;
using AiSdk.UiStreamProtocol.Models;

var client = new UiStreamClient(httpClient);

await foreach (var part in client.GetStreamAsync("https://my-service/api/chat"))
{
    if (part is TextDelta delta)
        Console.Write(delta.Delta);
}
```

To collect the full stream into a single message object:

```csharp
var assembler = new UiMessageAssembler(role: "assistant");

await foreach (var part in client.GetStreamAsync(url))
    assembler.AddPart(part);

UIMessage message = assembler.GetMessage();
```

---

### Writing stream parts directly

For cases where you need full control without a framework, write parts to any `Stream` via `UiSseWriter`:

```csharp
using AiSdk.UiStreamProtocol;
using AiSdk.UiStreamProtocol.Models;

var writer = new UiSseWriter();
await writer.WriteAsync(outputStream, new TextStart("text-1"));
await writer.WriteAsync(outputStream, new TextDelta("text-1", "Hello world!"));
await writer.WriteAsync(outputStream, new TextEnd("text-1"));
await writer.WriteAsync(outputStream, new Finish());
await writer.WriteAsync(outputStream, new Done());
```

## Stream parts

| Category | Types |
|---|---|
| Text | `TextStart`, `TextDelta`, `TextEnd` |
| Reasoning | `ReasoningStart`, `ReasoningDelta`, `ReasoningEnd` |
| Tool calls | `ToolInputStart`, `ToolInputDelta`, `ToolInputAvailable`, `ToolOutputAvailable` |
| Steps | `StartStep`, `FinishStep` |
| Sources | `SourceUrl`, `SourceDocument` |
| File | `File` |
| Custom data | `Data<T>` |
| Control | `Finish`, `Abort`, `Done` |
| Error | `Error` |

## License

MIT
