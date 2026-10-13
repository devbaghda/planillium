using System.Net;
using System.Text;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>AI client (2026-10-13) against a local stub server standing in for api.anthropic.com.</summary>
public sealed class AiClientTests : IDisposable
{
    private const string Key = "sk-ant-TESTKEY-123456";
    private readonly HttpListener _l = new();
    private readonly string _url;
    private Func<HttpListenerContext, Task> _handler = _ => Task.CompletedTask;
    public string? LastBody, LastKeyHeader, LastVersion;

    public AiClientTests()
    {
        var port = new Random().Next(20000, 40000);
        while (true)
        {
            try { _l.Prefixes.Clear(); _l.Prefixes.Add($"http://127.0.0.1:{port}/"); _l.Start(); break; }
            catch (HttpListenerException) { port++; }
        }
        _url = $"http://127.0.0.1:{port}";
        _ = Task.Run(async () =>
        {
            while (_l.IsListening)
            {
                HttpListenerContext c;
                try { c = await _l.GetContextAsync(); } catch { return; }
                _ = Task.Run(async () =>
                {
                    using (var r = new StreamReader(c.Request.InputStream, Encoding.UTF8)) LastBody = await r.ReadToEndAsync();
                    LastKeyHeader = c.Request.Headers["x-api-key"];
                    LastVersion = c.Request.Headers["anthropic-version"];
                    try { await _handler(c); } catch { }
                });
            }
        });
    }

    public void Dispose() => _l.Close();

    private AiClient Client(TimeSpan? idle = null) =>
        new(Key, "claude-test", _url) { IdleTimeout = idle ?? TimeSpan.FromSeconds(10) };

    private static async Task Sse(HttpListenerContext c, string stop, params string[] pieces)
    {
        c.Response.ContentType = "text/event-stream";
        var sb = new StringBuilder();
        foreach (var p in pieces)
            sb.Append("event: content_block_delta\ndata: ").Append(System.Text.Json.JsonSerializer.Serialize(
                new { type = "content_block_delta", delta = new { type = "text_delta", text = p } })).Append("\n\n");
        sb.Append("data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"" + stop + "\"}}\n\n");
        var b = Encoding.UTF8.GetBytes(sb.ToString());
        await c.Response.OutputStream.WriteAsync(b);
        c.Response.Close();
    }

    private static Task Status(HttpListenerContext c, int code, string body = "{\"error\":{\"message\":\"x\"}}")
    {
        c.Response.StatusCode = code;
        var b = Encoding.UTF8.GetBytes(body);
        c.Response.OutputStream.Write(b);
        c.Response.Close();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Success_JoinsStreamedPieces_AndSendsKeyOnlyInHeader()
    {
        _handler = c => Sse(c, "end_turn", "Hel", "lo ", "wörld");
        var r = await Client().CompleteAsync("my prompt");
        Assert.Equal("Hello wörld", r);
        Assert.Equal(Key, LastKeyHeader);
        Assert.Equal("2023-06-01", LastVersion);
        Assert.Contains("my prompt", LastBody);
        Assert.Contains("claude-test", LastBody);
        Assert.DoesNotContain(Key, LastBody);
    }

    [Theory]
    [InlineData(401, AiFailure.Auth)]
    [InlineData(403, AiFailure.Auth)]
    [InlineData(429, AiFailure.RateLimited)]
    [InlineData(400, AiFailure.BadResponse)]
    [InlineData(500, AiFailure.Server)]
    [InlineData(529, AiFailure.Server)]
    public async Task HttpErrors_MapToPlainFailures_WithoutLeakingKey(int code, AiFailure kind)
    {
        _handler = c => Status(c, code);
        var ex = await Assert.ThrowsAsync<AiException>(() => Client().CompleteAsync("p"));
        Assert.Equal(kind, ex.Kind);
        Assert.DoesNotContain(Key, ex.Message);
    }

    [Fact]
    public async Task GarbageBody_IsBadResponse()
    {
        _handler = async c => { c.Response.ContentType = "text/event-stream"; await c.Response.OutputStream.WriteAsync("nonsense\n\n"u8.ToArray()); c.Response.Close(); };
        var ex = await Assert.ThrowsAsync<AiException>(() => Client().CompleteAsync("p"));
        Assert.Equal(AiFailure.BadResponse, ex.Kind);
    }

    [Fact]
    public async Task CutOffReply_IsRejected()
    {
        _handler = c => Sse(c, "max_tokens", "partial");
        var ex = await Assert.ThrowsAsync<AiException>(() => Client().CompleteAsync("p"));
        Assert.Equal(AiFailure.BadResponse, ex.Kind);
    }

    [Fact]
    public async Task Silence_TimesOut()
    {
        _handler = async c => { await Task.Delay(5000); try { c.Response.Close(); } catch { } };
        var ex = await Assert.ThrowsAsync<AiException>(() => Client(TimeSpan.FromMilliseconds(400)).CompleteAsync("p"));
        Assert.Equal(AiFailure.Timeout, ex.Kind);
    }

    [Fact]
    public async Task Cancel_IsReportedAsCancelled()
    {
        _handler = async c => { await Task.Delay(5000); try { c.Response.Close(); } catch { } };
        using var cts = new CancellationTokenSource(300);
        var ex = await Assert.ThrowsAsync<AiException>(() => Client().CompleteAsync("p", cts.Token));
        Assert.Equal(AiFailure.Cancelled, ex.Kind);
    }

    [Fact]
    public async Task NoKey_FailsBeforeAnyRequest()
    {
        var ex = await Assert.ThrowsAsync<AiException>(() => new AiClient("", "m", _url).CompleteAsync("p"));
        Assert.Equal(AiFailure.NoKey, ex.Kind);
        Assert.Null(LastBody);
    }
}
