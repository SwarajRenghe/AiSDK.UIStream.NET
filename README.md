### `README.md`

````markdown
# AiSdk.UiStreamProtocol

This repository provides a .NET implementation of the **UI Stream Protocol** for interacting with frontend clients like the Vercel AI SDK. It is designed to facilitate streaming data between backend services and the frontend using Server-Sent Events (SSE) format. The stream supports both **text streams** and **data streams**, handling a variety of content types such as messages, reasoning, errors, and custom data.

## Features

- **Text Stream Protocol**: Supports sending text content in chunks, streamed incrementally to the frontend.
- **Data Stream Protocol**: Supports streaming various types of structured data, including messages, reasoning, tool input/output, and custom data.
- **SSE Format**: Utilizes the Server-Sent Events (SSE) protocol for efficient, one-way communication over HTTP.
- **Extensibility**: Allows adding custom data types and streaming formats for more advanced use cases.

## Project Structure

The project is organized into three main packages, each focusing on different aspects of the protocol.

### 1) **AiSdk.UiStreamProtocol (Core)**

This package contains the core logic for working with the UI stream protocol, including:

- **Stream-Part Types**: Defines strongly-typed stream parts (e.g., `TextStart`, `TextDelta`, `ReasoningStart`, etc.).
- **JSON Serialization/Deserialization**: Uses `System.Text.Json` to serialize and deserialize stream parts.
- **SSE Framing**: Handles the creation and parsing of SSE frames for communication.
- **Message Aggregation** (Optional): Assembles stream parts into a complete message as they arrive.

#### Public Surface:
- `UiStreamPart`: Base class for all stream parts.
- `UiSseWriter`: Writes SSE frames to a stream.
- `UiSseReader`: Reads SSE frames from a stream.
- `UiMessageAssembler`: Optionally aggregates stream parts into full messages.

### 2) **AiSdk.UiStreamProtocol.AspNetCore**

This package provides ASP.NET Core-specific functionality for serving UI streams:

- **Headers and Content Type**: Configures the correct headers for streaming (`Content-Type: text/event-stream`).
- **Minimal API & MVC Helpers**: Simplifies integration with ASP.NET Core.
- **Compression/Buffering Safety**: Ensures safe streaming by disabling compression (as per Vercel's recommendations).

#### Public Surface:
- `UiMessageStreamResult`: Implements the `IResult` interface for minimal APIs.
- `HttpResponse.WriteUiStreamAsync`: Helper method to write the stream to an HTTP response.
- `UiStreamOptions`: Options for configuring the stream (e.g., keep-alive interval, JSON settings).

### 3) **AiSdk.UiStreamProtocol.HttpClient**

This package provides a way to consume UI streams over HTTP, making it easier to integrate the protocol into console apps or services:

- **IAsyncEnumerable**: Provides async enumeration of `UiStreamPart` or `UiMessage` objects as they arrive.
- **Simple Integration**: Works out of the box with custom backend implementations.

## Getting Started

### Prerequisites

- **.NET SDK**: Make sure you have the .NET 6.0 SDK or higher installed on your machine.
- **Vercel AI SDK**: If you plan to use this package in conjunction with the Vercel AI SDK, ensure it is properly set up in your frontend.

### Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/your-username/AiSdk.UiStreamProtocol.git
   cd AiSdk.UiStreamProtocol
````

2. Restore NuGet packages:

   ```bash
   dotnet restore
   ```

3. Build the project:

   ```bash
   dotnet build
   ```

4. Run the tests (if applicable):

   ```bash
   dotnet test
   ```

### Usage Example

#### Backend (ASP.NET Core)

```csharp
using AiSdk.UiStreamProtocol.Models;

public class ChatController : ControllerBase
{
    [HttpPost("/api/chat")]
    public async Task<IActionResult> Chat([FromBody] List<UIMessage> messages)
    {
        // Stream each message's parts as they arrive
        foreach (var message in messages)
        {
            foreach (var part in message.Parts)
            {
                // Write each part as an SSE frame
                await Response.WriteAsync($"data: {JsonSerializer.Serialize(part)}\n\n");
            }
        }
        return Ok();
    }
}
```

#### Frontend (React + Vercel AI SDK)

```tsx
'use client';

import { useChat } from '@ai-sdk/react';
import { TextStreamChatTransport } from 'ai';
import { useState } from 'react';

export default function Chat() {
  const [input, setInput] = useState('');
  const { messages, sendMessage } = useChat({
    transport: new TextStreamChatTransport({ api: '/api/chat' }),
  });

  return (
    <div className="chat-container">
      {messages.map(message => (
        <div key={message.id}>
          {message.role === 'user' ? 'User: ' : 'AI: '}
          {message.parts.map((part, i) => {
            if (part.type === 'text') {
              return <div key={i}>{part.text}</div>;
            }
          })}
        </div>
      ))}

      <form onSubmit={e => {
        e.preventDefault();
        sendMessage({ text: input });
        setInput('');
      }}>
        <input
          value={input}
          placeholder="Say something..."
          onChange={e => setInput(e.target.value)}
        />
      </form>
    </div>
  );
}
```

## Stream Parts

This protocol defines various stream parts that can be used to structure the data being streamed:

* **Text**: `text-start`, `text-delta`, `text-end`
* **Reasoning**: `reasoning-start`, `reasoning-delta`, `reasoning-end`
* **Source**: `source-url`, `source-document`
* **File**: `file`
* **Custom Data**: `data-*` (e.g., `data-weather`, `data-stock`)
* **Error**: `error`
* **Tool**: `tool-input-start`, `tool-input-delta`, `tool-output-available`
* **Step**: `start-step`, `finish-step`
* **Finish/Abort**: `finish`, `abort`
* **Stream Termination**: `[DONE]`

## Contributing

We welcome contributions! If you find a bug or would like to request a feature, feel free to open an issue or submit a pull request. Please make sure your code adheres to the project's coding conventions and passes all tests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
