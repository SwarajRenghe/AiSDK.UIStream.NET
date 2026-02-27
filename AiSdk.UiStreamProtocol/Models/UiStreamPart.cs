namespace AiSdk.UiStreamProtocol.Models
{
    public abstract record UiStreamPart
    {
        public abstract string Type { get; }
    }

    // Text Stream Parts
    public record TextStart(string Id) : UiStreamPart
    {
        public override string Type => "text-start";
    }

    public record TextDelta(string Id, string Delta) : UiStreamPart
    {
        public override string Type => "text-delta";
    }

    public record TextEnd(string Id) : UiStreamPart
    {
        public override string Type => "text-end";
    }

    // Reasoning Stream Parts
    public record ReasoningStart(string Id) : UiStreamPart
    {
        public override string Type => "reasoning-start";
    }

    public record ReasoningDelta(string Id, string Delta) : UiStreamPart
    {
        public override string Type => "reasoning-delta";
    }

    public record ReasoningEnd(string Id) : UiStreamPart
    {
        public override string Type => "reasoning-end";
    }

    // Source Stream Parts
    public record SourceUrl(string SourceId, string Url) : UiStreamPart
    {
        public override string Type => "source-url";
    }

    public record SourceDocument(string SourceId, string MediaType, string Title) : UiStreamPart
    {
        public override string Type => "source-document";
    }

    // File Part
    public record File(string Url, string MediaType) : UiStreamPart
    {
        public override string Type => "file";
    }

    // Custom Data Stream Parts
    // Note: the spec embeds the data subtype in the "type" field (e.g. "data-weather"),
    // but this representation uses a separate DataType field. Use the Type property
    // override in a subclass for spec-exact type names.
    public record Data<T>(string DataType, T DataContent) : UiStreamPart
    {
        public override string Type => "data";
    }

    // Error Part
    public record Error(string ErrorText) : UiStreamPart
    {
        public override string Type => "error";
    }

    // Tool Input Stream Parts
    public record ToolInputStart(string ToolCallId, string ToolName) : UiStreamPart
    {
        public override string Type => "tool-input-start";
    }

    public record ToolInputDelta(string ToolCallId, string Delta) : UiStreamPart
    {
        public override string Type => "tool-input-delta";
    }

    public record ToolInputAvailable(string ToolCallId, string ToolName, object Input) : UiStreamPart
    {
        public override string Type => "tool-input-available";
    }

    public record ToolOutputAvailable(string ToolCallId, object Output) : UiStreamPart
    {
        public override string Type => "tool-output-available";
    }

    // Step Stream Parts
    public record StartStep() : UiStreamPart
    {
        public override string Type => "start-step";
    }

    public record FinishStep() : UiStreamPart
    {
        public override string Type => "finish-step";
    }

    // Finish Stream Part
    public record Finish() : UiStreamPart
    {
        public override string Type => "finish";
    }

    // Abort Stream Part
    public record Abort(string Reason) : UiStreamPart
    {
        public override string Type => "abort";
    }

    // Stream Termination — serialized as the literal "data: [DONE]" by UiSseWriter
    public record Done() : UiStreamPart
    {
        public override string Type => "[DONE]";
    }
}
