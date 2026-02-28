# AiSdk.UiStreamProtocol.AspNetCore

ASP.NET Core integration for the [Vercel AI SDK UI Stream Protocol](https://ai-sdk.dev/docs/ai-sdk-ui/stream-protocol). Lets you stream typed AI responses from a .NET backend directly to a frontend using `useChat`.

## Installation

```sh
dotnet add package AiSdk.UiStreamProtocol.AspNetCore
```

## Usage

Return a `UiMessageStreamResult` from any minimal API endpoint. It sets all required headers (`Content-Type: text/event-stream`, `x-vercel-ai-ui-message-stream: v1`, and buffering/compression headers) and streams the body.

```csharp
using AiSdk.UiStreamProtocol;
using AiSdk.UiStreamProtocol.AspNetCore;
using AiSdk.UiStreamProtocol.Models;

app.MapPost("/api/chat", () =>
{
    return new UiMessageStreamResult(async stream =>
    {
        var writer = new UiSseWriter();
        var textId = Guid.NewGuid().ToString();

        await writer.WriteAsync(stream, new TextStart(textId));

        // stream tokens from your LLM here
        await writer.WriteAsync(stream, new TextDelta(textId, "Hello world"));

        await writer.WriteAsync(stream, new TextEnd(textId));
        await writer.WriteAsync(stream, new Finish());
        await writer.WriteAsync(stream, new Done());
    });
});
```

Then on the frontend:

```tsx
import { useChat } from '@ai-sdk/react';
import { UIMessageStreamTransport } from 'ai';

const { messages } = useChat({
  transport: new UIMessageStreamTransport({ api: '/api/chat' }),
});
```

[Full documentation and source](https://github.com/SwarajRenghe/Dotnet-Vercel-UIStream-Protocol)
