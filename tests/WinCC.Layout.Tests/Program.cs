using System.Text;
using System.Text.Json;
using System.Net;
using WinCC.Integrations;
using WinCC.Layout;

// Dependency-free contract runner: nonzero exit on failure; never reports a zero-test success.
int passed = 0, failed = 0;
string root = Path.Combine(Path.GetTempPath(), "wincc-contracts-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Widget Clock(string id = "clock-1") => new(id, "clock", "primary", new(32, 32, 260, 180), 0, []);
LayoutDocument WithClock()
{
    var d = LayoutDocument.Empty(); d.Active.Widgets.Add(Clock()); return d;
}
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + e.Message); }
}
string FileFor(string name) => Path.Combine(root, name, "layout.json");
try
{
    Test("Blank canvas round-trips without adding a widget (C-012)", () =>
        Check(LayoutJson.Parse(LayoutJson.Serialize(LayoutDocument.Empty())).Active.Widgets.Count == 0));
    Test("All catalog widgets accept their documented default sizes", () =>
    {
        var d = LayoutDocument.Empty();
        foreach (var s in WidgetCatalog.All)
            d.Active.Widgets.Add(new(Guid.NewGuid().ToString("N"), s.Type, "primary", new(0, 0, s.Width, s.Height), 0, []));
        Check(LayoutJson.Parse(LayoutJson.Serialize(d)).Active.Widgets.Count == 7);
    });
    Test("Startup is Normal Mode; mutations are rejected (C-001, C-002)", () =>
    {
        var s = new EditorSession(WithClock()); Check(!s.IsEditing);
        Throws<InvalidOperationException>(() => s.Update(Clock() with { ZIndex = 1 }));
        Throws<InvalidOperationException>(() => s.Change(d => d.Active.Widgets.Clear()));
        Throws<InvalidOperationException>(s.Undo); Throws<InvalidOperationException>(s.Cancel);
    });
    Test("External snapshots cannot mutate the session", () =>
    {
        var s = new EditorSession(WithClock()); s.Current.Active.Widgets.Clear();
        Check(s.Current.Active.Widgets.Count == 1);
        s.Enter(); s.Current.Active.Widgets[0].Settings["hourFormat"] = "24";
        Check(s.Current.Active.Widgets[0].Settings.Count == 0);
    });
    Test("Mutation callbacks cannot retain mutable references into the session", () =>
    {
        var s = new EditorSession(WithClock()); s.Enter(); LayoutDocument? retained = null;
        s.Change(d => { d.Active.Widgets.Add(Clock("second")); retained = d; });
        retained!.Active.Widgets.Clear(); Check(s.Current.Active.Widgets.Count == 2);
    });
    Test("Add/remove/reorder mutate only drafts (C-013, C-014)", () =>
    {
        var s = new EditorSession(WithClock()); s.Enter();
        s.Change(d => d.Active.Widgets.Add(Clock("second") with { ZIndex = 1 }));
        Check(s.IsDirty && s.Current.Active.Widgets.Count == 2);
        s.Change(d => d.Active.Widgets.RemoveAt(0)); Check(s.Current.Active.Widgets[0].Id == "second");
        s.Cancel(); Check(s.Current.Active.Widgets.Single().Id == "clock-1");
    });
    Test("Cancel restores geometry, collection and settings (C-008)", () =>
    {
        var s = new EditorSession(WithClock()); var original = LayoutJson.Serialize(s.Current); s.Enter();
        s.Update(Clock() with { RectDip = new(80, 90, 300, 200), Settings = new() { ["hourFormat"] = "24" } });
        s.Cancel(); Check(!s.IsEditing && original.SequenceEqual(LayoutJson.Serialize(s.Current)));
    });
    Test("Undo/redo restores complete draft transactions", () =>
    {
        var s = new EditorSession(WithClock()); s.Enter();
        s.Update(Clock() with { RectDip = new(80, 90, 300, 200) });
        s.Undo(); Check(s.Current.Active.Widgets[0].RectDip.X == 32 && s.CanRedo);
        s.Redo(); Check(s.Current.Active.Widgets[0].RectDip.X == 80 && s.CanUndo);
    });
    Test("A new edit after undo clears redo", () =>
    {
        var s = new EditorSession(WithClock()); s.Enter();
        s.Update(Clock() with { ZIndex = 1 }); s.Undo(); s.Update(Clock() with { ZIndex = 2 });
        Check(!s.CanRedo);
    });
    Test("History is bounded at 100 layout transactions", () =>
    {
        var s = new EditorSession(WithClock()); s.Enter();
        for (int i = 1; i <= 150; i++) s.Update(Clock() with { ZIndex = i });
        int undo = 0; while (s.CanUndo) { s.Undo(); undo++; }
        Check(undo == 100 && s.Current.Active.Widgets[0].ZIndex == 50);
    });
    Test("Invalid mutations leave the session and history unchanged", () =>
    {
        var s = new EditorSession(WithClock()); s.Enter();
        Throws<InvalidDataException>(() => s.Change(d => d.Active.Widgets.Add(Clock())));
        Check(!s.CanUndo && !s.IsDirty && s.Current.Active.Widgets.Count == 1);
    });
    Test("Done saves durably, resets mode/history, survives restart (C-009)", () =>
    {
        var file = FileFor("done"); var store = new LayoutStore(file); var s = new EditorSession(WithClock());
        s.Enter(); s.Update(Clock() with { RectDip = new(100, 100, 300, 200) });
        Check(!File.Exists(file)); s.Done(store);
        Check(!s.IsEditing && !s.CanUndo);
        var restarted = new EditorSession(new LayoutStore(file).Load().Document);
        Check(!restarted.IsEditing && restarted.Current.Active.Widgets[0].RectDip.X == 100);
    });
    Test("Failed Done retains Edit Mode and retryable draft (C-010)", () =>
    {
        var s = new EditorSession(WithClock()); s.Enter(); s.Update(Clock() with { ZIndex = 2 });
        Throws<IOException>(() => s.Done(new FailingStore()));
        Check(s.IsEditing && s.IsDirty && s.CanUndo && s.Current.Active.Widgets[0].ZIndex == 2);
        s.Done(new LayoutStore(FileFor("retry"))); Check(!s.IsEditing);
    });
    Test("Real filesystem write failure retains draft and leaves saved data unchanged", () =>
    {
        string blocker = Path.Combine(root, "not-a-directory"); File.WriteAllText(blocker, "preserve");
        var s = new EditorSession(WithClock()); s.Enter(); s.Update(Clock() with { ZIndex = 3 });
        Throws<IOException>(() => s.Done(new LayoutStore(Path.Combine(blocker, "layout.json"))));
        Check(s.IsEditing && s.IsDirty && File.ReadAllText(blocker) == "preserve");
    });
    Test("Crash during editing leaves durable layout untouched (C-011)", () =>
    {
        var file = FileFor("crash"); var store = new LayoutStore(file); store.Save(WithClock());
        var s = new EditorSession(store.Load().Document); s.Enter(); s.Update(Clock() with { ZIndex = 5 });
        var restarted = new EditorSession(new LayoutStore(file).Load().Document);
        Check(!restarted.IsEditing && restarted.Current.Active.Widgets[0].ZIndex == 0);
    });
    Test("Atomic replacement retains a valid prior backup and no temp files", () =>
    {
        var file = FileFor("backup"); var store = new LayoutStore(file); store.Save(WithClock()); store.Save(LayoutDocument.Empty());
        Check(LayoutJson.Parse(File.ReadAllBytes(file + ".bak")).Active.Widgets.Count == 1);
        Check(store.Load().Document.Active.Widgets.Count == 0);
        Check(Directory.GetFiles(Path.GetDirectoryName(file)!, "*.tmp").Length == 0);
    });
    Test("Malformed layout recovers backup without overwriting original (G2-007)", () =>
    {
        var file = FileFor("corrupt"); var store = new LayoutStore(file); store.Save(WithClock()); store.Save(LayoutDocument.Empty());
        File.WriteAllText(file, "{bad"); var recovery = new LayoutStore(file); var loaded = recovery.Load();
        Check(loaded.Warning is not null && loaded.ReadOnly && loaded.Document.Active.Widgets.Count == 1);
        Throws<IOException>(() => recovery.Save(loaded.Document)); Check(File.ReadAllText(file) == "{bad");
    });
    Test("Unsupported future schema is preserved and cannot be saved over (G2-008)", () =>
    {
        var file = FileFor("future"); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        const string future = "{\"schemaVersion\":99,\"futureField\":true}"; File.WriteAllText(file, future);
        var store = new LayoutStore(file); var loaded = store.Load();
        Check(loaded.ReadOnly && loaded.Warning is not null);
        Throws<IOException>(() => store.Save(LayoutDocument.Empty())); Check(File.ReadAllText(file) == future);
    });
    Test("External future-version changes after load are protected", () =>
    {
        var file = FileFor("external"); var store = new LayoutStore(file); store.Save(WithClock()); _ = store.Load();
        File.WriteAllText(file, "{\"schemaVersion\":2}");
        Throws<UnsupportedSchemaException>(() => store.Save(LayoutDocument.Empty()));
    });
    Test("Missing layout produces valid blank canvas", () =>
        Check(new LayoutStore(FileFor("missing")).Load().Document.Active.Widgets.Count == 0));
    Test("Duplicate widget IDs rejected (G2-009)", () =>
    {
        var d = WithClock(); d.Active.Widgets.Add(Clock()); Throws<InvalidDataException>(() => LayoutJson.Serialize(d));
    });
    Test("Unknown widget type rejected", () =>
    {
        var d = WithClock(); d.Active.Widgets[0] = Clock() with { Type = "execute" };
        Throws<InvalidDataException>(() => LayoutValidator.Validate(d));
    });
    Test("NaN, infinity, negative and oversized geometry rejected", () =>
    {
        foreach (var r in new[] { new RectDip(double.NaN, 0, 260, 180), new(0, double.PositiveInfinity, 260, 180),
                     new(-1, 0, 260, 180), new(0, 0, 10, 180), new(0, 0, 5000, 180) })
        {
            var d = WithClock(); d.Active.Widgets[0] = Clock() with { RectDip = r };
            Throws<InvalidDataException>(() => LayoutValidator.Validate(d));
        }
    });
    Test("Null collections and malformed JSON are rejected safely", () =>
    {
        Throws<InvalidDataException>(() => LayoutJson.Parse(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"activeProfileId\":\"default\",\"profiles\":null}")));
        Throws<JsonException>(() => LayoutJson.Parse(Encoding.UTF8.GetBytes("{")));
    });
    Test("Unknown fields are rejected rather than silently discarded", () =>
        Throws<JsonException>(() => LayoutJson.Parse(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"activeProfileId\":\"default\",\"profiles\":[],\"unknown\":1}"))));
    Test("Oversized file and excessive widget count rejected", () =>
    {
        Throws<InvalidDataException>(() => LayoutJson.Parse(new byte[LayoutJson.MaxBytes + 1]));
        var d = LayoutDocument.Empty();
        for (int i = 0; i < 101; i++) d.Active.Widgets.Add(Clock("clock-" + i));
        Throws<InvalidDataException>(() => LayoutValidator.Validate(d));
    });
    Test("Clock hour format validates", () =>
    {
        var d = WithClock(); d.Active.Widgets[0].Settings["hourFormat"] = "wrong";
        Throws<InvalidDataException>(() => LayoutValidator.Validate(d));
    });
    Test("Negative monitor origins and mixed DPI convert monitor-local coordinates (D-002, D-003)", () =>
    {
        var m = new MonitorArea("left", -2560, -1440, 1706, 960, 1.5, false);
        var p = Geometry.ToPhysical(new(100, 80, 260, 180), m);
        Check(p.X == -2410 && p.Y == -1320);
    });
    Test("Clamping keeps widgets entirely within work area (C-017)", () =>
    {
        var m = new MonitorArea("primary", 0, 0, 800, 600, 1, true);
        var r = Geometry.Clamp(new(790, -50, 400, 250), WidgetCatalog.Get("clock"), m);
        Check(r.X == 400 && r.Y == 0 && r.Width == 400 && r.Height == 250);
    });
    Test("Disconnected monitor uses runtime projection without destroying original association", () =>
    {
        var w = Clock() with { MonitorKey = "disconnected", RectDip = new(2000, 900, 300, 180) };
        var m = Geometry.Resolve(w.MonitorKey, [new("primary-display", -1920, 0, 1920, 1040, 1, true)]);
        var projected = Geometry.Clamp(w.RectDip, WidgetCatalog.Get(w.Type), m);
        Check(projected.X + projected.Width <= 1920 && w.MonitorKey == "disconnected" && w.RectDip.X == 2000);
    });
    Test("Tiny display runtime projection is reachable", () =>
    {
        var r = Geometry.Clamp(Clock().RectDip, WidgetCatalog.Get("clock"), new("tiny", 0, 0, 160, 100, 2, true));
        Check(r == new RectDip(0, 0, 160, 100));
    });
    Test("Notes save independently of Cancel and survive restart (C-003)", () =>
    {
        string id = Guid.NewGuid().ToString("N"); var notes = new NotesStore(Path.Combine(root, "notes"));
        var s = new EditorSession(WithClock()); s.Enter(); notes.Save(id, "Hello\nworld ✦"); s.Cancel();
        Check(new NotesStore(Path.Combine(root, "notes")).Load(id) == "Hello\nworld ✦");
    });
    Test("Note paths reject traversal and content is bounded", () =>
    {
        var notes = new NotesStore(Path.Combine(root, "notes"));
        Throws<InvalidDataException>(() => notes.Save("../escape", "data"));
        Throws<InvalidDataException>(() => notes.Save(Guid.NewGuid().ToString("N"), new string('x', NotesStore.MaxCharacters + 1)));
    });
    Test("Non-object layout roots are rejected safely", () =>
    {
        foreach (string json in new[] { "null", "[]", "1", "true", "\"text\"", "{\"schemaVersion\":\"1\"}", "{\"schemaVersion\":null}" })
            Throws<InvalidDataException>(() => LayoutJson.Parse(Encoding.UTF8.GetBytes(json)));
    });
    Test("Weather request sends only chosen coordinates and parses real API shape", () =>
    {
        using var http = new HttpClient(new FakeHandler(request =>
        {
            Check(request.RequestUri!.Host == "api.open-meteo.com" && request.RequestUri.Query.Contains("latitude=43.7") &&
                  request.RequestUri.Query.Contains("longitude=-79.4") && request.RequestUri.Query.Contains("fahrenheit"));
            Check(request.Headers.Authorization is null);
            return new(HttpStatusCode.OK) { Content = new StringContent("""
                {"current":{"temperature_2m":50.5,"weather_code":3,"time":"2026-11-05T12:00"},
                 "daily":{"time":["2026-11-05"],"temperature_2m_min":[42.0],"temperature_2m_max":[55.0]}}
                """) };
        }));
        var weather = new WeatherClient(http).Fetch(43.7, -79.4, true, CancellationToken.None).GetAwaiter().GetResult();
        Check(weather.Temperature == 50.5 && weather.Unit == "°F" && weather.Conditions == "Overcast" && weather.Forecast.Count == 1);
    });
    Test("Invalid weather coordinates never send a request", () =>
    {
        using var http = new HttpClient(new FakeHandler(_ => throw new Exception("Unexpected network request")));
        Throws<ArgumentOutOfRangeException>(() => new WeatherClient(http).Fetch(91, 0, false, CancellationToken.None).GetAwaiter().GetResult());
        Throws<ArgumentOutOfRangeException>(() => new WeatherClient(http).Fetch(0, double.NaN, false, CancellationToken.None).GetAwaiter().GetResult());
    });
    Test("Weather malformed provider shape reports unavailable", () =>
    {
        using var http = new HttpClient(new FakeHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent("{}") }));
        Throws<ProviderException>(() => new WeatherClient(http).Fetch(0, 0, false, CancellationToken.None).GetAwaiter().GetResult());
    });
    Test("AI request uses fixed destination, opted-in history, no storage or tools", () =>
    {
        using var http = new HttpClient(new FakeHandler(request =>
        {
            Check(request.RequestUri!.AbsoluteUri == "https://api.openai.com/v1/responses");
            Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == "unit-test-placeholder");
            using var payload = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            Check(!payload.RootElement.GetProperty("store").GetBoolean() && !payload.RootElement.TryGetProperty("tools", out _));
            Check(payload.RootElement.GetProperty("input")[0].GetProperty("content").GetString() == "Hello");
            return new(HttpStatusCode.OK) { Content = new StringContent("""
                {"output":[{"type":"message","content":[{"type":"output_text","text":"Response"}]}]}
                """) };
        }));
        var answer = new AiClient(http).Send("unit-test-placeholder", "test-model", [new("user", "Hello")], CancellationToken.None).GetAwaiter().GetResult();
        Check(answer == "Response");
    });
    Test("AI missing key and command-like message roles never send a request", () =>
    {
        using var http = new HttpClient(new FakeHandler(_ => throw new Exception("Unexpected network request")));
        var client = new AiClient(http);
        Throws<ProviderException>(() => client.Send("", "test", [new("user", "Hello")], CancellationToken.None).GetAwaiter().GetResult());
        Throws<ProviderException>(() => client.Send("unit-test-placeholder", "test", [new("execute", "Hello")], CancellationToken.None).GetAwaiter().GetResult());
        Throws<ProviderException>(() => client.Send("bad\nkey", "test", [new("user", "Hello")], CancellationToken.None).GetAwaiter().GetResult());
    });
    Test("Provider denial, rate limits and redirects remain explicit errors", () =>
    {
        foreach (var code in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.Redirect })
        {
            using var response = new HttpResponseMessage(code) { Content = new StringContent("unused") };
            Throws<ProviderException>(() => ProviderHttp.Read(response, CancellationToken.None).GetAwaiter().GetResult());
        }
    });
    Test("Provider bodies are bounded and requests honor cancellation", () =>
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 1024 * 1024 + 1)) };
        Throws<ProviderException>(() => ProviderHttp.Read(response, CancellationToken.None).GetAwaiter().GetResult());
        using var http = new HttpClient(new FakeHandler(_ => throw new Exception("Canceled requests should not execute")));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Throws<OperationCanceledException>(() => new AiClient(http).Send("unit-test-placeholder", "test", [new("user", "Hello")], cancellation.Token).GetAwaiter().GetResult());
    });
}
finally { Directory.Delete(root, recursive: true); }
Console.WriteLine($"{passed} passed, {failed} failed; Windows UI/native checks NOT RUN.");
return failed == 0 && passed > 0 ? 0 : 1;

sealed class FailingStore : ILayoutStore
{
    public void Save(LayoutDocument document) => throw new IOException("Simulated disk failure");
}
sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
}
