using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WinCC.Integrations;

public sealed class ProviderException(string message) : IOException(message);
public sealed record ForecastDay(string Date, double Minimum, double Maximum);
public sealed record WeatherReading(double Temperature, string Unit, string Conditions, string ObservedAt, IReadOnlyList<ForecastDay> Forecast);
public sealed record ChatMessage(string Role, string Text);

public static class ProviderHttp
{
    public static async Task<JsonDocument> Read(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (!response.IsSuccessStatusCode)
            throw new ProviderException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Permission denied. Check your provider access.",
                HttpStatusCode.TooManyRequests => "Provider rate limit reached. Try again later.",
                _ => $"Provider returned HTTP {(int)response.StatusCode}."
            });
        const int limit = 1024 * 1024;
        if (response.Content.Headers.ContentLength > limit) throw new ProviderException("Provider response is too large.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellation);
        using var output = new MemoryStream(); byte[] buffer = new byte[8192];
        while (true)
        {
            int read = await input.ReadAsync(buffer, cancellation); if (read == 0) break;
            if (output.Length + read > limit) throw new ProviderException("Provider response is too large.");
            output.Write(buffer, 0, read);
        }
        return JsonDocument.Parse(output.ToArray());
    }
    public static HttpClient CreateClient() => new(new HttpClientHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(30) };
}

public sealed class WeatherClient(HttpClient http)
{
    public async Task<WeatherReading> Fetch(double latitude, double longitude, bool fahrenheit, CancellationToken cancellation)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 || !double.IsFinite(longitude) || longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Enter valid latitude and longitude.");
        string lat = latitude.ToString(CultureInfo.InvariantCulture), lon = longitude.ToString(CultureInfo.InvariantCulture);
        var uri = new Uri($"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
            "&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min&forecast_days=3&timezone=auto" +
            (fahrenheit ? "&temperature_unit=fahrenheit" : ""));
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation);
        using var data = await ProviderHttp.Read(response, cancellation);
        try
        {
            var root = data.RootElement; var current = root.GetProperty("current");
            var daily = root.GetProperty("daily");
            var forecast = new List<ForecastDay>();
            for (int i = 0; i < Math.Min(3, daily.GetProperty("time").GetArrayLength()); i++)
                forecast.Add(new(daily.GetProperty("time")[i].GetString()!,
                    daily.GetProperty("temperature_2m_min")[i].GetDouble(), daily.GetProperty("temperature_2m_max")[i].GetDouble()));
            double temperature = current.GetProperty("temperature_2m").GetDouble();
            if (!double.IsFinite(temperature)) throw new ProviderException("Weather temperature is unavailable.");
            return new(temperature, fahrenheit ? "°F" : "°C", Condition(current.GetProperty("weather_code").GetInt32()),
                current.GetProperty("time").GetString() ?? "", forecast);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException or IndexOutOfRangeException)
        { throw new ProviderException("Weather provider returned an unsupported response."); }
    }
    public static string Condition(int code) => code switch
    {
        0 => "Clear", 1 or 2 => "Partly cloudy", 3 => "Overcast", 45 or 48 => "Fog",
        >= 51 and <= 57 => "Drizzle", >= 61 and <= 67 => "Rain", >= 71 and <= 77 => "Snow",
        >= 80 and <= 82 => "Rain showers", 85 or 86 => "Snow showers", >= 95 and <= 99 => "Thunderstorm", _ => "Unavailable"
    };
}

public sealed class AiClient(HttpClient http)
{
    // Fixed HTTPS destination; no arbitrary endpoint, remote tools, shell commands or persisted key.
    public async Task<string> Send(string apiKey, string model, IReadOnlyList<ChatMessage> messages, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ProviderException("Set WINCC_AI_API_KEY before starting WinCC to connect AI.");
        if (apiKey.Length > 2048 || apiKey.Any(char.IsControl)) throw new ProviderException("AI key format is invalid. Update the process environment and restart.");
        if (string.IsNullOrWhiteSpace(model) || model.Length > 100) throw new ProviderException("Enter a model name.");
        if (messages.Count is < 1 or > 20 || messages.Any(m => m.Role is not ("user" or "assistant") || m.Text.Length is < 1 or > 8000))
            throw new ProviderException("Chat history exceeds the supported size. Clear the conversation and try again.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model, store = false, max_output_tokens = 1200,
            input = messages.Select(m => new { role = m.Role, content = m.Text })
        }), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        using var data = await ProviderHttp.Read(response, cancellation);
        if (!data.RootElement.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            throw new ProviderException("AI provider returned no readable response.");
        var result = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
            foreach (var part in content.EnumerateArray())
                if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text" && part.TryGetProperty("text", out var text))
                    result.AppendLine(text.GetString());
        }
        if (result.Length == 0) throw new ProviderException("AI provider returned no readable response.");
        if (result.Length > 8000) throw new ProviderException("AI response exceeded the supported conversation size.");
        return result.ToString().Trim();
    }
}
