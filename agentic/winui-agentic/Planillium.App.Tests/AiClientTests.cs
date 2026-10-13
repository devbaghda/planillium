using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Planillium.App.Services;

namespace Planillium.App.Tests;

/// <summary>
/// AiClient (ai-layer spec) against a local HttpListener stub. No test here touches Credential
/// Manager or the real Anthropic API: the key is injected through the keyProvider, and every
/// request goes to 127.0.0.1 on a port the stub owns. The key used is a fake.
/// </summary>
public sealed class AiClientTests : IClassFixture<TestRootFixture>
{
    private const string FakeKey = "sk-ant-FAKE-test-0001";

    private static AiClient Client(string baseUrl, TimeSpan? timeout = null, string? key = FakeKey) =>
        new(new HttpClient { Timeout = Timeout.InfiniteTimeSpan }, baseUrl, AiSettings.DefaultModel,
            () => key, timeout ?? TimeSpan.FromSeconds(10));

    private static string TextReply(string text) =>
        JsonSerializer.Serialize(new
        {
            id = "msg_FAKE",
            type = "message",
            role = "assistant",
            content = new[] { new { type = "text", text } },
            stop_reason = "end_turn",
        });

    [Fact]
    public async Task Request_has_the_documented_shape_and_headers()
    {
        using var stub = new StubAnthropic();
        stub.Body = TextReply("ok");

        var result = await Client(stub.BaseUrl).AskAsync("Plan the week.", CancellationToken.None);

        Assert.True(result.Succeeded);
        var req = Assert.Single(stub.Requests);
        Assert.Equal("POST", req.Method);
        Assert.Equal("/v1/messages", req.Path);
        Assert.Equal(FakeKey, req.ApiKey);
        Assert.Equal("2023-06-01", req.Version);
        Assert.StartsWith("application/json", req.ContentType);

        using var doc = JsonDocument.Parse(req.Body);
        var root = doc.RootElement;
        var names = root.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "max_tokens", "messages", "model" }, names);
        Assert.Equal(AiSettings.DefaultModel, root.GetProperty("model").GetString());
        Assert.Equal(AiClient.MaxTokens, root.GetProperty("max_tokens").GetInt32());
        var message = Assert.Single(root.GetProperty("messages").EnumerateArray());
        Assert.Equal("user", message.GetProperty("role").GetString());
        Assert.Equal("Plan the week.", message.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Reply_text_blocks_are_concatenated_in_order()
    {
        using var stub = new StubAnthropic();
        stub.Body = JsonSerializer.Serialize(new
        {
            content = new object[]
            {
                new { type = "text", text = "Hel" },
                new { type = "thinking", thinking = "ignored" },
                new { type = "text", text = "lo" },
            },
            stop_reason = "end_turn",
        });

        var result = await Client(stub.BaseUrl).AskAsync("p", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Hello", result.Text);
    }

    [Fact]
    public async Task Test_connection_sends_sixteen_max_tokens()
    {
        using var stub = new StubAnthropic();
        stub.Body = TextReply("OK");

        var result = await Client(stub.BaseUrl).AskAsync(AiSettings.TestConnectionPrompt, CancellationToken.None,
            AiSettings.TestConnectionMaxTokens);

        Assert.True(result.Succeeded);
        var req = Assert.Single(stub.Requests);
        using var doc = JsonDocument.Parse(req.Body);
        Assert.Equal(16, doc.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Theory]
    [InlineData(400, AiFailure.BadRequest400)]
    [InlineData(413, AiFailure.BadRequest400)]
    [InlineData(401, AiFailure.Rejected401)]
    [InlineData(404, AiFailure.ModelNotFound404)]
    [InlineData(429, AiFailure.RateLimited429)]
    [InlineData(500, AiFailure.ServerError5xx)]
    [InlineData(503, AiFailure.ServerError5xx)]
    [InlineData(418, AiFailure.Unreadable)]
    public async Task Http_status_maps_to_its_failure_category(int status, AiFailure expected)
    {
        using var stub = new StubAnthropic();
        stub.Status = status;
        stub.Body = "{\"type\":\"error\",\"error\":{\"type\":\"x\",\"message\":\"secret detail\"}}";

        var result = await Client(stub.BaseUrl).AskAsync("p", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.Failure);
        Assert.DoesNotContain("secret detail", result.Message);
        Assert.DoesNotContain(FakeKey, result.Message);
    }

    [Fact]
    public async Task Slow_reply_past_the_timeout_is_a_timeout_not_a_cancel()
    {
        using var stub = new StubAnthropic();
        stub.DelayMs = 5000;
        stub.Body = TextReply("late");

        var result = await Client(stub.BaseUrl, TimeSpan.FromMilliseconds(300))
            .AskAsync("p", CancellationToken.None);

        Assert.Equal(AiFailure.Timeout, result.Failure);
    }

    [Fact]
    public async Task Stop_during_a_call_is_a_cancel()
    {
        using var stub = new StubAnthropic();
        stub.DelayMs = 5000;
        stub.Body = TextReply("late");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var result = await Client(stub.BaseUrl).AskAsync("p", cts.Token);

        Assert.Equal(AiFailure.Cancelled, result.Failure);
        Assert.Equal("Stopped. Nothing was changed.", result.Message);
    }

    [Fact]
    public async Task Reply_cut_off_at_max_tokens_is_refused_even_with_text()
    {
        using var stub = new StubAnthropic();
        stub.Body = JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = "{\"id\": \"half" } },
            stop_reason = "max_tokens",
        });

        var result = await Client(stub.BaseUrl).AskAsync("p", CancellationToken.None);

        Assert.Equal(AiFailure.Truncated, result.Failure);
    }

    [Theory]
    [InlineData("{\"content\":[],\"stop_reason\":\"end_turn\"}")]
    [InlineData("{\"content\":[{\"type\":\"text\",\"text\":\"   \"}],\"stop_reason\":\"end_turn\"}")]
    public async Task Empty_reply_is_EmptyReply(string body)
    {
        using var stub = new StubAnthropic();
        stub.Body = body;

        var result = await Client(stub.BaseUrl).AskAsync("p", CancellationToken.None);

        Assert.Equal(AiFailure.EmptyReply, result.Failure);
    }

    [Fact]
    public async Task Non_json_body_is_Unreadable()
    {
        using var stub = new StubAnthropic();
        stub.Body = "<html>gateway</html>";

        var result = await Client(stub.BaseUrl).AskAsync("p", CancellationToken.None);

        Assert.Equal(AiFailure.Unreadable, result.Failure);
    }

    [Fact]
    public async Task Error_shaped_body_on_success_status_is_Unreadable()
    {
        using var stub = new StubAnthropic();
        stub.Body = "{\"error\":{\"type\":\"overloaded_error\",\"message\":\"busy\"}}";

        var result = await Client(stub.BaseUrl).AskAsync("p", CancellationToken.None);

        Assert.Equal(AiFailure.Unreadable, result.Failure);
    }

    [Fact]
    public async Task Unreachable_host_is_Network()
    {
        var port = FreePort();

        var result = await Client($"http://127.0.0.1:{port}").AskAsync("p", CancellationToken.None);

        Assert.Equal(AiFailure.Network, result.Failure);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task No_key_makes_no_request(string? key)
    {
        using var stub = new StubAnthropic();
        stub.Body = TextReply("ok");

        var result = await Client(stub.BaseUrl, key: key).AskAsync("p", CancellationToken.None);

        Assert.Equal(AiFailure.NoKey, result.Failure);
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task The_key_never_reaches_the_log_file()
    {
        using var stub = new StubAnthropic();
        stub.Status = 401;
        stub.Body = "{\"error\":{\"message\":\"invalid x-api-key\"}}";

        var result = await Client(stub.BaseUrl).AskAsync("p", CancellationToken.None);

        Assert.Equal(AiFailure.Rejected401, result.Failure);
        foreach (var file in Directory.EnumerateFiles(AppPaths.Root, "*", SearchOption.AllDirectories))
        {
            var text = ReadSharedText(file);
            Assert.DoesNotContain(FakeKey, text);
        }
    }

    private static string ReadSharedText(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd();
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private sealed record Captured(string Method, string Path, string? ApiKey, string? Version,
        string? ContentType, string Body);

    /// <summary>A one-purpose HTTP stub on 127.0.0.1. Records every request; answers with the
    /// configured status, body, and optional delay.</summary>
    private sealed class StubAnthropic : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;

        public StubAnthropic()
        {
            var port = FreePort();
            BaseUrl = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();
            _loop = Loop();
        }

        public string BaseUrl { get; }
        public int Status { get; set; } = 200;
        public string Body { get; set; } = "";
        public int DelayMs { get; set; }
        public ConcurrentQueue<Captured> Requests { get; } = new();

        private async Task Loop()
        {
            while (!_stop.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }
                _ = Handle(ctx);
            }
        }

        private async Task Handle(HttpListenerContext ctx)
        {
            try
            {
                string body;
                using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    body = await reader.ReadToEndAsync();
                Requests.Enqueue(new Captured(ctx.Request.HttpMethod, ctx.Request.Url!.AbsolutePath,
                    ctx.Request.Headers["x-api-key"], ctx.Request.Headers["anthropic-version"],
                    ctx.Request.ContentType, body));

                if (DelayMs > 0) await Task.Delay(DelayMs, _stop.Token);

                var bytes = Encoding.UTF8.GetBytes(Body);
                ctx.Response.StatusCode = Status;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
            catch
            {
                // The client gave up (timeout or cancel) or the stub is being disposed.
                try { ctx.Response.Abort(); } catch { /* already closed */ }
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            try { _listener.Stop(); } catch { /* already stopped */ }
            _listener.Close();
        }
    }
}
