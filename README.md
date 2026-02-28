# .NET Implementation of the Vercel AI SDK UI Stream Protocol (Data Stream Protocol)

[![CI](https://github.com/SwarajRenghe/Dotnet-Vercel-UIStream-Protocol/actions/workflows/ci.yml/badge.svg)](https://github.com/SwarajRenghe/Dotnet-Vercel-UIStream-Protocol/actions/workflows/ci.yml)

A .NET implementation of the [Vercel AI SDK UI Stream Protocol](https://sdk.vercel.ai/docs/ai-sdk-ui/stream-protocol). Use this to build a .NET backend that streams AI responses directly to a frontend using `useChat`.

The protocol uses Server-Sent Events (SSE) with typed stream parts — `text-delta`, `tool-input-start`, `finish`, etc. — that the Vercel AI SDK knows how to consume.

## Packages

| Package                             | NuGet                                                                                                                                           | Description                                                         |
| ----------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| `AiSdk.UiStreamProtocol`            | [![NuGet](https://img.shields.io/nuget/v/AiSdk.UiStreamProtocol)](https://www.nuget.org/packages/AiSdk.UiStreamProtocol/)                       | Core types, SSE reader/writer, message assembler                    |
| `AiSdk.UiStreamProtocol.AspNetCore` | [![NuGet](https://img.shields.io/nuget/v/AiSdk.UiStreamProtocol.AspNetCore)](https://www.nuget.org/packages/AiSdk.UiStreamProtocol.AspNetCore/) | ASP.NET Core helpers (headers, `IResult`, `HttpResponse` extension) |
| `AiSdk.UiStreamProtocol.HttpClient` | [![NuGet](https://img.shields.io/nuget/v/AiSdk.UiStreamProtocol.HttpClient)](https://www.nuget.org/packages/AiSdk.UiStreamProtocol.HttpClient/) | Consume a UI stream from another service as `IAsyncEnumerable`      |

## Installation

Most projects only need the AspNetCore package, which pulls in the core as a dependency:

```sh
dotnet add package AiSdk.UiStreamProtocol.AspNetCore
```

If you're consuming a stream from another service (e.g. a console app or a BFF calling a microservice):

```sh
dotnet add package AiSdk.UiStreamProtocol.HttpClient
```

## Usage

### Streaming from an ASP.NET Core endpoint

The simplest way is to return a `UiMessageStreamResult` from a minimal API endpoint. It sets all the required headers and streams the body.

```csharp
using AiSdk.UiStreamProtocol;
using AiSdk.UiStreamProtocol.Models;

app.MapPost("/api/chat", () =>
{
    return new UiMessageStreamResult(async stream =>
    {
        var writer = new UiSseWriter();

        await writer.WriteAsync(stream, new TextStart("text-1"));
        await writer.WriteAsync(stream, new TextDelta("text-1", "Hello "));
        await writer.WriteAsync(stream, new TextDelta("text-1", "world!"));
        await writer.WriteAsync(stream, new TextEnd("text-1"));
        await writer.WriteAsync(stream, new Finish());
        await writer.WriteAsync(stream, new Done());
    });
});
```

You can also write directly to the response using the `WriteUiStreamAsync` extension:

```csharp
app.MapPost("/api/chat", async (HttpResponse response) =>
{
    await response.WriteUiStreamAsync(async writer =>
    {
        await writer.WriteAsync(response.Body, new TextDelta("text-1", "Hi there!"));
        await writer.WriteAsync(response.Body, new Done());
    });
});
```

### Connecting to the frontend

On the React side, point `useChat` at your endpoint:

```tsx
import { useChat } from "@ai-sdk/react";
import { TextStreamChatTransport } from "ai";

const { messages, sendMessage } = useChat({
  transport: new TextStreamChatTransport({ api: "/api/chat" }),
});
```

### Consuming a stream from another .NET service

```csharp
var client = new UiStreamClient(httpClient);

await foreach (var part in client.GetStreamAsync("https://my-service/api/chat"))
{
    if (part is TextDelta delta)
        Console.Write(delta.Delta);
}
```

### Assembling a full message from stream parts

If you need to collect all parts into a single `UIMessage` object:

```csharp
var assembler = new UiMessageAssembler(role: "assistant");

await foreach (var part in client.GetStreamAsync(url))
{
    assembler.AddPart(part);
}

UIMessage message = assembler.GetMessage();
```

## Stream parts

The full set of stream part types:

| Category    | Types                                                                           |
| ----------- | ------------------------------------------------------------------------------- |
| Text        | `TextStart`, `TextDelta`, `TextEnd`                                             |
| Reasoning   | `ReasoningStart`, `ReasoningDelta`, `ReasoningEnd`                              |
| Tool calls  | `ToolInputStart`, `ToolInputDelta`, `ToolInputAvailable`, `ToolOutputAvailable` |
| Steps       | `StartStep`, `FinishStep`                                                       |
| Sources     | `SourceUrl`, `SourceDocument`                                                   |
| File        | `File`                                                                          |
| Custom data | `Data<T>`                                                                       |
| Control     | `Finish`, `Abort`, `Done`                                                       |
| Error       | `Error`                                                                         |

## License

MIT
