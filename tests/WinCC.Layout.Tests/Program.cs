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
                     new(-1, 0, 260, 180), new(0, 0, 10, 180), new(0, 0, 100001, 180) })
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
    Test("Glass preserves the live centre and masks rounded exterior", () =>
    {
        var source = new byte[100 * 75 * 4];
        for (int i = 0; i < source.Length; i += 4) { source[i] = 40; source[i + 1] = 80; source[i + 2] = 120; }
        var result = GlassOptics.Render(source, 100, 75, 80, 60);
        int centre = (30 * 80 + 40) * 4;
        Check(result[3] == 0 && result[centre + 3] == 255);
        Check(result[centre] == 40 && result[centre + 1] == 80 && result[centre + 2] == 120);
    });
    Test("Glass refraction changes real source samples, not just a tint", () =>
    {
        var source = new byte[200 * 150 * 4];
        for (int y = 0; y < 150; y++) for (int x = 0; x < 200; x++)
        {
            int i = (y * 200 + x) * 4;
            source[i] = (byte)((x / 4 % 2) * 200); source[i + 1] = (byte)((y / 4 % 2) * 200); source[i + 2] = (byte)x;
        }
        var plain = GlassOptics.Render(source, 200, 150, 160, 120, 0);
        var refracted = GlassOptics.Render(source, 200, 150, 160, 120, 1);
        Check(plain.Zip(refracted).Count(pair => pair.First != pair.Second) > 500);
        int middle = (60 * 160 + 80) * 4;
        Check(plain[middle] == refracted[middle]);
        Check(source.Any(v => v != 0));
    });
    Test("Glass responds to changing background and rejects invalid frames", () =>
    {
        var dark = new byte[100 * 75 * 4]; var bright = Enumerable.Repeat((byte)255, dark.Length).ToArray();
        int centre = (30 * 80 + 40) * 4;
        Check(GlassOptics.Render(dark, 100, 75, 80, 60)[centre] == 0);
        Check(GlassOptics.Render(bright, 100, 75, 80, 60)[centre] == 255);
        Throws<ArgumentOutOfRangeException>(() => GlassOptics.Render(new byte[10], 100, 75, 80, 60));
        Throws<ArgumentOutOfRangeException>(() => GlassOptics.Render(dark, 100, 75, 80, 60, double.NaN));
    });

    Test("Clear glass uses localized dimming beneath bright foreground content", () =>
    {
        var source = Enumerable.Repeat((byte)200, 200 * 150 * 4).ToArray();
        var result = GlassOptics.Render(source, 200, 150, 160, 120, 1, new GlassAppearance());
        int center = (60 * 160 + 80) * 4, outsideLabel = (20 * 160 + 80) * 4;
        Check(result[center] < result[outsideLabel]);
        Check(result[center] >= 160 && result[outsideLabel] <= 205);
    });
    Test("Interactive lighting and material size alter the optical surface", () =>
    {
        var source = new byte[200 * 150 * 4];
        for (int i = 0; i < source.Length; i++) source[i] = (byte)(i % 251);
        var quiet = GlassOptics.Render(source, 200, 150, 160, 120, 1, new GlassAppearance());
        var active = GlassOptics.Render(source, 200, 150, 160, 120, 1, new GlassAppearance(MaterialScale: 2, Interaction: 1, LightX: .7, LightY: .7));
        Check(quiet.Zip(active).Count(p => p.First != p.Second) > 500);
        Throws<ArgumentOutOfRangeException>(() => GlassOptics.Render(source, 200, 150, 160, 120, 1, new GlassAppearance(Interaction: double.NaN)));
    });
    Test("Regular, frosted and high-contrast material variants remain distinct", () =>
    {
        var source = Enumerable.Repeat((byte)220, 200 * 150 * 4).ToArray();
        var clear = GlassOptics.Render(source, 200, 150, 160, 120, 1, new GlassAppearance());
        var regular = GlassOptics.Render(source, 200, 150, 160, 120, 1, new GlassAppearance(Clear: false));
        var frosted = GlassOptics.Render(source, 200, 150, 160, 120, 1, new GlassAppearance(ReducedTransparency: true));
        var contrast = GlassOptics.Render(source, 200, 150, 160, 120, 1, new GlassAppearance(HighContrast: true));
        int center = (60 * 160 + 80) * 4;
        Check(clear[center] != regular[center] && frosted[center] < clear[center]);
        Check(contrast[center] == 12 && contrast[center + 1] == 12 && contrast[center + 2] == 12);
        Check(contrast[3] == 0);
    });

    Test("Living glass changes its silhouette over time without moving its content centre", () =>
    {
        Check(GlassShape.Distance(140, 75, 280, 150, 0, 0) < -20);
        Check(GlassShape.Distance(140, 75, 280, 150, 2, 1) < -20);
        var a = GlassShape.Outline(280, 150, 0, 0);
        var b = GlassShape.Outline(280, 150, 2, 1);
        Check(a.Zip(b).Count(p => Math.Abs(p.First.X - p.Second.X) + Math.Abs(p.First.Y - p.Second.Y) > .002) > 60);
        foreach (var point in b)
        {
            Check(point.X > 0 && point.X < 1 && point.Y > 0 && point.Y < 1);
            Check(Math.Abs(GlassShape.Distance(point.X * 280, point.Y * 150, 280, 150, 2, 1)) < .01);
        }
    });
    Test("Opaque-host compositing does not turn clear edges black", () =>
    {
        var source = Enumerable.Repeat((byte)255, 200 * 150 * 4).ToArray();
        var pixels = GlassOptics.Render(source, 200, 150, 160, 120, 1,
            new GlassAppearance(Organic: true, Phase: 1.2, CompositeToBackdrop: true));
        for (int i = 0; i < pixels.Length; i += 4)
        {
            Check(pixels[i + 3] == 255);
            Check(pixels[i] >= 200 && pixels[i + 1] >= 200 && pixels[i + 2] >= 200);
        }
        Check(pixels[0] == 255);
    });

    Test("FPS deadlines include frame work and uncapped adds no delay", () =>
    {
        Check(Math.Abs(FramePacing.Delay(2, 2.010, 60).TotalMilliseconds - 6.6667) < .001);
        Check(FramePacing.Delay(2, 2.100, 60) == TimeSpan.Zero);
        Check(FramePacing.Delay(2, 2, 0) == TimeSpan.Zero);
        Check(FramePacing.Delay(2, 2, 120) < FramePacing.Delay(2, 2, 30));
        Throws<ArgumentOutOfRangeException>(() => FramePacing.Delay(0, 0, 501));
        Throws<ArgumentOutOfRangeException>(() => FramePacing.Delay(double.NaN, 0, 60));
    });
    Test("Rigid glass preserves its silhouette across time and hover lighting", () =>
    {
        var source = Enumerable.Repeat((byte)180, 200 * 150 * 4).ToArray();
        var first = GlassOptics.Render(source, 200, 150, 160, 120, 1, new GlassAppearance(Phase: 0));
        var later = GlassOptics.Render(source, 200, 150, 160, 120, 1, new GlassAppearance(Phase: 10, Interaction: 1, LightX: .7));
        for (int i = 3; i < first.Length; i += 4) Check(first[i] == later[i]);
        Check(!first.SequenceEqual(later));
    });
    Test("Geometry cache does not reuse source pixels or stale dimensions", () =>
    {
        var source = Enumerable.Repeat((byte)180, 200 * 150 * 4).ToArray();
        var first = GlassOptics.Render(source, 200, 150, 160, 120);
        _ = GlassOptics.Render(source, 200, 150, 80, 60);
        var repeated = GlassOptics.Render(source, 200, 150, 160, 120);
        Check(first.SequenceEqual(repeated));
        Array.Fill(source, (byte)20);
        Check(!first.SequenceEqual(GlassOptics.Render(source, 200, 150, 160, 120)));
    });

    Test("Reusable optical buffers overwrite old pixels and never mutate source", () =>
    {
        var source = Enumerable.Repeat((byte)180, 200 * 150 * 4).ToArray();
        var output = Enumerable.Repeat((byte)255, 160 * 120 * 4).ToArray();
        var returned = GlassOptics.Render(source, 200, 150, 160, 120, 1, new GlassAppearance(), output);
        Check(ReferenceEquals(output, returned) && output[3] == 0);
        Check(source.All(v => v == 180));
        Throws<ArgumentException>(() => GlassOptics.Render(source, 200, 150, 160, 120, 1, null, new byte[8]));
    });

    Test("Three preset slots persist independent layouts and reject extra slots", () =>
    {
        var presets = new PresetStore(Path.Combine(root, "presets"));
        Check(!presets.Exists(1));
        presets.Save(1, WithClock()); presets.Save(2, LayoutDocument.Empty());
        var third = WithClock(); third.Active.Widgets[0] = Clock() with { RectDip = new(120, 80, 360, 200) };
        presets.Save(3, third);
        Check(new PresetStore(Path.Combine(root, "presets")).Load(3).Active.Widgets[0].RectDip.Width == 360);
        Check(presets.Load(1).Active.Widgets.Count == 1 && presets.Load(2).Active.Widgets.Count == 0);
        Throws<ArgumentOutOfRangeException>(() => presets.Save(4, WithClock()));
        Throws<ArgumentOutOfRangeException>(() => presets.Load(0));
        File.WriteAllText(Path.Combine(root, "presets", "preset-2.json"), "corrupt");
        Throws<IOException>(() => presets.Load(2));
        Check(File.ReadAllText(Path.Combine(root, "presets", "preset-2.json")) == "corrupt");
    });
    Test("Time-shaped mask refracts along digit edges and leaves holes clear", () =>
    {
        const int width = 80, height = 60;
        var mask = new byte[width * height];
        for (int y = 10; y < 40; y++) for (int x = 20; x < 50; x++)
            if (x < 26 || x > 43 || y < 16 || y > 33) mask[y * width + x] = 255;
        var source = Enumerable.Repeat((byte)180, 100 * 75 * 4).ToArray();
        var pixels = GlassOptics.Render(source, 100, 75, width, height, 1, new GlassAppearance(), shapeMask: mask);
        Check(pixels[(20 * width + 22) * 4 + 3] == 255);
        Check(pixels[(25 * width + 35) * 4 + 3] == 0);
        Check(pixels[3] == 0);
        mask = (byte[])mask.Clone(); Array.Clear(mask);
        var blank = GlassOptics.Render(source, 100, 75, width, height, 1, new GlassAppearance(), shapeMask: mask);
        Check(blank.All(v => v == 0));
    });

    Test("Clock design validates customization and rejects malformed values", () =>
    {
        var design = new ClockDesign { Font = "VT323", Weight = 800, StretchX = 1.5, StretchY = .7, Stroke = 2, Tracking = 3, Tint = "#CFA4FF", TintAmount = .12, ShowDate = false, TimeFormat = "HH:mm:ss" };
        design.Validate();
        var restored = JsonSerializer.Deserialize<ClockDesign>(JsonSerializer.Serialize(design))!;
        Check(restored == design);
        Throws<InvalidDataException>(() => (design with { Tint = "red" }).Validate());
        Throws<InvalidDataException>(() => (design with { StretchX = double.NaN }).Validate());
        Throws<InvalidDataException>(() => (design with { Font = "https://example.com/font" }).Validate());
        Throws<InvalidDataException>(() => (design with { DateFormat = "%" }).Validate());
    });
    Test("50/50 anchors the widget centre and scales with the display", () =>
    {
        var m = new MonitorArea("left", -2560, 0, 1600, 1000, 1.5, true);
        var centered = Geometry.AtPercent(new(0,0,280,150), WidgetCatalog.Get("clock"), m, 50,50);
        Check(centered.X == 660 && centered.Y == 425);
        var larger = Geometry.AtPercent(centered, WidgetCatalog.Get("clock"), m with { WidthDip = 2000, HeightDip = 1200 },50,50);
        Check(larger.X == 860 && larger.Y == 525);
        var edge = Geometry.AtPercent(centered, WidgetCatalog.Get("clock"), m,0,100);
        Check(edge.X == 0 && edge.Y == 850);
        Throws<ArgumentOutOfRangeException>(() => Geometry.AtPercent(centered, WidgetCatalog.Get("clock"), m,101,50));
    });
    Test("Saved anchors survive layout restart and invalid anchors are rejected", () =>
    {
        var d = WithClock(); d.Active.Widgets[0].Settings["anchorX"] = "50"; d.Active.Widgets[0].Settings["anchorY"] = "25.5";
        Check(LayoutJson.Parse(LayoutJson.Serialize(d)).Active.Widgets[0].Settings["anchorY"] == "25.5");
        d.Active.Widgets[0].Settings["anchorX"] = "NaN";
        Throws<InvalidDataException>(() => LayoutValidator.Validate(d));
    });
    Test("Subtle tint preserves coverage and affects the selected color channels", () =>
    {
        var source = Enumerable.Repeat((byte)150,200*150*4).ToArray();
        var untinted = GlassOptics.Render(source,200,150,160,120,1,new GlassAppearance());
        var tinted = GlassOptics.Render(source,200,150,160,120,1,new GlassAppearance(TintRgb:0xFF0000,TintAmount:.25));
        int center=(60*160+80)*4;
        Check(tinted[center+2] > tinted[center] && tinted[center+3] == untinted[center+3]);
        Check(Math.Abs(tinted[center+2]-untinted[center+2]) < 20);
        Throws<ArgumentOutOfRangeException>(() => GlassOptics.Render(source,200,150,160,120,1,new GlassAppearance(TintAmount:1)));
    });

    Test("Clock can fill a 4K or ultrawide work area without the old fixed cap", () =>
    {
        var spec = WidgetCatalog.Get("clock");
        var area = new MonitorArea("wide", 0, 0, 5120, 2120, 1, true);
        var fitted = Geometry.Clamp(new(0,0,5120,2120), spec, area);
        Check(fitted.Width == 5120 && fitted.Height == 2120);
        var document = WithClock(); document.Active.Widgets[0] = Clock() with { RectDip = fitted };
        LayoutValidator.Validate(document);
    });
    Test("Tint strength is effective, monotonic and preserves background detail", () =>
    {
        var source = Enumerable.Repeat((byte)180,200*150*4).ToArray();
        var low = GlassOptics.Render(source,200,150,160,120,1,new GlassAppearance(TintRgb:0xFF0000,TintAmount:.1));
        var high = GlassOptics.Render(source,200,150,160,120,1,new GlassAppearance(TintRgb:0xFF0000,TintAmount:.3));
        int center=(60*160+80)*4;
        Check(high[center] < low[center] - 25);
        Check(high[center+2] == low[center+2]);
        Array.Fill(source,(byte)90);
        var darker = GlassOptics.Render(source,200,150,160,120,1,new GlassAppearance(TintRgb:0xFF0000,TintAmount:.3));
        Check(darker[center+2] < high[center+2]);
    });
    Test("Neutral glass does not create rainbow channel splitting", () =>
    {
        var source = new byte[200*150*4];
        for(int y=0;y<150;y++) for(int x=0;x<200;x++)
        {
            int i=(y*200+x)*4; byte shade=(byte)(x*255/199);
            source[i]=source[i+1]=source[i+2]=shade;
        }
        var output=GlassOptics.Render(source,200,150,160,120,1,new GlassAppearance());
        for(int i=0;i<output.Length;i+=4)
            if(output[i+3]==255) Check(Math.Abs(output[i]-output[i+2])<=2);
    });

    Test("Google font metadata selects normal regular and rejects unsafe paths", () =>
    {
        string metadata = "fonts { style: \"italic\" weight: 400 filename: \"Family-Italic.ttf\" } fonts { style: \"normal\" weight: 400 filename: \"Family-Regular.ttf\" }";
        Check(GoogleFontMetadata.FileName(metadata) == "Family-Regular.ttf");
        Throws<InvalidDataException>(() => GoogleFontMetadata.FileName("fonts { filename: \"../escape.ttf\" }"));
        Throws<InvalidDataException>(() => GoogleFontMetadata.FileName("fonts { filename: \"https://example.com/font.ttf\" }"));
        Throws<InvalidDataException>(() => GoogleFontMetadata.FileName("not font metadata"));
    });

    Test("Glyph distances are radial rather than eight-direction chamfer facets", () =>
    {
        var mask = new byte[11*11]; mask[5*11+5]=255;
        var d = GlyphDistance.Build(mask,11,11);
        Check(Math.Abs(d[6*11+8] - (Math.Sqrt(10)-.5)) < .00001);
        Check(Math.Abs(d[8*11+6] - d[6*11+8]) < .00001);
        Check(d[5*11+5] < 0);
        Throws<ArgumentException>(() => GlyphDistance.Build(new byte[3],11,11));
    });

    Test("Glass stroke height rolls smoothly through its medial axis", () =>
    {
        const int w = 120, h = 120;
        var mask = new byte[w*h];
        for(int y=15;y<105;y++) for(int x=40;x<80;x++) mask[y*w+x]=255;
        var surface = GlyphSurface.Build(mask,w,h,20);
        double H(int x) => surface[60*w+x];
        double slopeLeft = (H(60)-H(58))*.5, slopeRight = (H(61)-H(59))*.5;
        double edgeSlope = (H(41)-H(39))*.5;
        Check(slopeLeft > 0 && slopeRight < 0);
        Check(Math.Abs(slopeLeft-slopeRight) < edgeSlope*.15);
        Check(Math.Abs(H(59)-H(60)) < .00001);
        Check(surface.All(v=>float.IsFinite(v) && v>=0 && v<=1));
        Check(mask[60*w+39]==0 && mask[60*w+40]==255); // Optical smoothing never changes coverage.
        Throws<ArgumentException>(()=>GlyphSurface.Build(mask,w,h,double.NaN));
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
