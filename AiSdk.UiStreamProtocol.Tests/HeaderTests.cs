using Microsoft.AspNetCore.Http;
using Xunit;

/// <summary>
/// Verifies that UiMessageStreamResult sets the response headers required by the
/// Vercel AI SDK UI Message Stream Protocol.
/// </summary>
public class HeaderTests
{
    private static async Task<HttpContext> RunAsync()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var result = new UiMessageStreamResult(_ => Task.CompletedTask);
        await result.ExecuteAsync(context);
        return context;
    }

    [Fact]
    public async Task Sets_ContentType_TextEventStream()
    {
        var ctx = await RunAsync();
        Assert.Equal("text/event-stream", ctx.Response.ContentType);
    }

    [Fact]
    public async Task Sets_UiMessageStream_Header_v1()
    {
        // Required by the spec: tells the Vercel AI SDK this is a UI message stream
        var ctx = await RunAsync();
        Assert.Equal("v1", ctx.Response.Headers["x-vercel-ai-ui-message-stream"].ToString());
    }

    [Fact]
    public async Task Sets_CacheControl_NoCacheNoTransform()
    {
        var ctx = await RunAsync();
        Assert.Equal("no-cache, no-transform", ctx.Response.Headers["Cache-Control"].ToString());
    }

    [Fact]
    public async Task Sets_XAccelBuffering_No()
    {
        // Disables nginx buffering so chunks reach the client immediately
        var ctx = await RunAsync();
        Assert.Equal("no", ctx.Response.Headers["X-Accel-Buffering"].ToString());
    }

    [Fact]
    public async Task Sets_ContentEncoding_None()
    {
        // Prevents proxies from compressing the stream, which would break SSE framing
        var ctx = await RunAsync();
        Assert.Equal("none", ctx.Response.Headers["Content-Encoding"].ToString());
    }
}
