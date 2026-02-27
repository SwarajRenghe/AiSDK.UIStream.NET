using AiSdk.UiStreamProtocol.Models;

namespace AiSdk.UiStreamProtocol;

public class UiMessageAssembler
{
    private readonly List<UiStreamPart> _parts = new();

    private readonly string _id;
    private readonly string _role;

    public UiMessageAssembler(string role, string? id = null)
    {
        _role = role;
        _id = id ?? Guid.NewGuid().ToString();
    }

    public void AddPart(UiStreamPart part)
    {
        _parts.Add(part);
    }

    public UIMessage GetMessage()
    {
        return new UIMessage(
            _id,
            _role,
            new List<UiStreamPart>(_parts), // defensive copy
            DateTime.UtcNow
        );
    }
}
