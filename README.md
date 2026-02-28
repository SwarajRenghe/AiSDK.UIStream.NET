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

Call any OpenAI-compatible provider and pass the response to `FromOpenAiStream` — it handles the SSE parsing and protocol translation automatically.

```sh
dotnet add package AiSdk.UiStreamProtocol.AspNetCore
```

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiSdk.UiStreamProtocol.AspNetCore;

app.MapPost("/api/chat", async (IConfiguration config) =>
{
    var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config["OpenRouter:ApiKey"]);
    request.Content = new StringContent(
        JsonSerializer.Serialize(new
        {
            model = "anthropic/claude-3.5-sonnet",
            stream = true,
            messages = new[] { new { role = "user", content = "Hello!" } }
        }),
        Encoding.UTF8, "application/json");

    var http = new HttpClient();
    var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();

    return UiMessageStreamResult.FromOpenAiStream(response);
});
```

Works with any OpenAI-compatible provider: OpenRouter, Azure OpenAI, OpenAI directly, etc. Reasoning tokens (`deepseek/deepseek-r1` and similar) are handled automatically.

On the React side, point `useChat` at your endpoint:

```tsx
import { useChat } from '@ai-sdk/react';
import { TextStreamChatTransport } from 'ai';

const { messages, sendMessage } = useChat({
  transport: new TextStreamChatTransport({ api: '/api/chat' }),
});
```

---

### Consuming a stream between .NET services

If a service already produces a UI stream, read it as an `IAsyncEnumerable` of typed parts:

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

### Writing stream parts manually

For full control over the output, write parts directly to any `Stream`:

```csharp
using AiSdk.UiStreamProtocol;
using AiSdk.UiStreamProtocol.Models;

var writer = new UiSseWriter();
await writer.WriteAsync(outputStream, new TextStart("text-0"));
await writer.WriteAsync(outputStream, new TextDelta("text-0", "Hello world!"));
await writer.WriteAsync(outputStream, new TextEnd("text-0"));
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
