using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

public class UiMessageStreamResult : IResult
{
    private readonly Func<Stream, Task> _writeStreamFunc;

    public UiMessageStreamResult(Func<Stream, Task> writeStreamFunc)
    {
        _writeStreamFunc = writeStreamFunc;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache, no-transform";
        httpContext.Response.Headers["x-vercel-ai-ui-message-stream"] = "v1";
        httpContext.Response.Headers["Content-Encoding"] = "none";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        await _writeStreamFunc(httpContext.Response.Body);
    }
}
