namespace AiSdk.UiStreamProtocol.Models;

public record UIMessage
{
    // Unique identifier for the message
    public string Id { get; init; }

    // The role of the message (e.g., "user", "AI", "system")
    public string Role { get; init; }

    // A collection of stream parts that make up the message (text, reasoning, etc.)
    public List<UiStreamPart> Parts { get; init; }

    // Metadata (optional, can be extended with other fields like timestamp, etc.)
    public DateTime Timestamp { get; init; }

    public UIMessage(string id, string role, List<UiStreamPart> parts, DateTime timestamp)
    {
        Id = id;
        Role = role;
        Parts = parts;
        Timestamp = timestamp;
    }
}
