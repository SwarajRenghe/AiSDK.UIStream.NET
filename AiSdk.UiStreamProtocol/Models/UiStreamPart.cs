namespace AiSdk.UiStreamProtocol.Models
{
    // Base class for all stream parts.
    public abstract record UiStreamPart
    {
        public string Type { get; init; } = string.Empty;
    }

    // Text Stream Parts
    public record TextStart(string Content) : UiStreamPart
    {
        public new string Type { get; init; } = "text-start";
    }

    public record TextDelta(string DeltaContent) : UiStreamPart
    {
        public new string Type { get; init; } = "text-delta";
    }

    public record TextEnd() : UiStreamPart
    {
        public new string Type { get; init; } = "text-end";
    }

    // Reasoning Stream Parts
    public record ReasoningStart(string Id) : UiStreamPart
    {
        public new string Type { get; init; } = "reasoning-start";
    }

    public record ReasoningDelta(string DeltaContent) : UiStreamPart
    {
        public new string Type { get; init; } = "reasoning-delta";
    }

    public record ReasoningEnd() : UiStreamPart
    {
        public new string Type { get; init; } = "reasoning-end";
    }

    // Source Stream Parts
    public record SourceUrl(string SourceId, string Url) : UiStreamPart
    {
        public new string Type { get; init; } = "source-url";
    }

    public record SourceDocument(string SourceId, string MediaType, string Title) : UiStreamPart
    {
        public new string Type { get; init; } = "source-document";
    }

    // File Part
    public record File(string Url, string MediaType) : UiStreamPart
    {
        public new string Type { get; init; } = "file";
    }

    // Custom Data Stream Parts (data-* type pattern)
    public record Data<T>(string DataType, T DataContent) : UiStreamPart
    {
        public new string Type { get; init; } = "data";
    }

    // Error Part
    public record Error(string ErrorText) : UiStreamPart
    {
        public new string Type { get; init; } = "error";
    }

    // Tool Input Stream Parts
    public record ToolInputStart(string ToolCallId, string ToolName) : UiStreamPart
    {
        public new string Type { get; init; } = "tool-input-start";
    }

    public record ToolInputDelta(string ToolCallId, string InputTextDelta) : UiStreamPart
    {
        public new string Type { get; init; } = "tool-input-delta";
    }

    public record ToolInputAvailable(string ToolCallId, string ToolName, object Input) : UiStreamPart
    {
        public new string Type { get; init; } = "tool-input-available";
    }

    public record ToolOutputAvailable(string ToolCallId, object Output) : UiStreamPart
    {
        public new string Type { get; init; } = "tool-output-available";
    }

    // Step Stream Parts
    public record StartStep() : UiStreamPart
    {
        public new string Type { get; init; } = "start-step";
    }

    public record FinishStep() : UiStreamPart
    {
        public new string Type { get; init; } = "finish-step";
    }

    // Finish Stream Part
    public record Finish() : UiStreamPart
    {
        public new string Type { get; init; } = "finish";
    }

    // Abort Stream Part
    public record Abort(string Reason) : UiStreamPart
    {
        public new string Type { get; init; } = "abort";
    }

    // Stream Termination
    public record Done() : UiStreamPart
    {
        public new string Type { get; init; } = "[DONE]";
    }
}
