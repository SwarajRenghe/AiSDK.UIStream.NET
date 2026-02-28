# AiSdk.UiStreamProtocol.HttpClient

`HttpClient` integration for consuming a [Vercel AI SDK UI Stream Protocol](https://ai-sdk.dev/docs/ai-sdk-ui/stream-protocol) endpoint from .NET — useful in BFFs, microservices, or console tools that need to read a UI stream from another service.

## Installation

```sh
dotnet add package AiSdk.UiStreamProtocol.HttpClient
```

## Usage

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

To collect all parts into a single assembled message:

```csharp
using AiSdk.UiStreamProtocol;

var assembler = new UiMessageAssembler(role: "assistant");

await foreach (var part in client.GetStreamAsync(url))
    assembler.AddPart(part);

var message = assembler.GetMessage();
```

[Full documentation and source](https://github.com/SwarajRenghe/Dotnet-Vercel-UIStream-Protocol)
