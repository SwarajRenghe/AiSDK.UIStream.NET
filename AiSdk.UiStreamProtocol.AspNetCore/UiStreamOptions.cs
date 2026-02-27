using System.Text.Json;

public class UiStreamOptions
{
    public int KeepAliveInterval { get; set; } = 30; // Seconds
    public JsonSerializerOptions JsonOptions { get; set; } = new JsonSerializerOptions();
    public bool UseCompression { get; set; } = false;
}
