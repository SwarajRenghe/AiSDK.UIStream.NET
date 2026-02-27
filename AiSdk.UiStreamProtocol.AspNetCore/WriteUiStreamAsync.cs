public static class HttpResponseExtensions
{
    public static async Task WriteUiStreamAsync(this HttpResponse response, Func<UiSseWriter, Task> writeAction)
    {
        var writer = new UiSseWriter();
        await writeAction(writer);
    }
}
