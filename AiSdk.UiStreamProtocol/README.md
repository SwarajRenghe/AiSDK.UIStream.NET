# AiSdk.UiStreamProtocol

Core types and SSE primitives for the [Vercel AI SDK UI Stream Protocol](https://ai-sdk.dev/docs/ai-sdk-ui/stream-protocol).

This package is the foundation — it defines all stream part types and handles SSE framing. Most projects won't use it directly and should instead install the integration package for their use case:

- **ASP.NET Core server** → [`AiSdk.UiStreamProtocol.AspNetCore`](https://www.nuget.org/packages/AiSdk.UiStreamProtocol.AspNetCore/)
- **HTTP client / consumer** → [`AiSdk.UiStreamProtocol.HttpClient`](https://www.nuget.org/packages/AiSdk.UiStreamProtocol.HttpClient/)

## Writing SSE frames

```csharp
using AiSdk.UiStreamProtocol;
using AiSdk.UiStreamProtocol.Models;

var writer = new UiSseWriter();

await writer.WriteAsync(stream, new TextStart("text-1"));
await writer.WriteAsync(stream, new TextDelta("text-1", "Hello world"));
await writer.WriteAsync(stream, new TextEnd("text-1"));
await writer.WriteAsync(stream, new Finish());
await writer.WriteAsync(stream, new Done());
```

Each call writes one SSE frame: `data: {...}\n\n`. `Done` writes the literal `data: [DONE]\n\n`.

## Stream part types

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

[Full documentation and source](https://github.com/SwarajRenghe/Dotnet-Vercel-UIStream-Protocol)
