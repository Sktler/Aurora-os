using System.Net.Http.Headers;
using System.Text.Json;
using Aurora.Core;

namespace Aurora.Android.Services;

public sealed class NwsWeatherProvider : IWeatherProvider
{
    private readonly HttpClient _http;

    public NwsWeatherProvider(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Aurora", "1.0"));
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/geo+json"));
    }

    public async Task<WeatherData> GetWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var point = await GetJsonAsync($"https://api.weather.gov/points/{latitude:F4},{longitude:F4}", cancellationToken);
        var properties = point.GetProperty("properties");
        var forecastUrl = properties.GetProperty("forecast").GetString()!;
        var stationUrl = properties.GetProperty("observationStations").GetString()!;
        var city = properties.TryGetProperty("relativeLocation", out var relative) && relative.ValueKind == JsonValueKind.Object
            ? relative.GetProperty("properties").GetProperty("city").GetString() ?? "Current location"
            : "Current location";

        var forecast = await GetJsonAsync(forecastUrl, cancellationToken);
        var periods = forecast.GetProperty("properties").GetProperty("periods");
        var period = periods.GetArrayLength() > 0 ? periods[0] : default;

        var stationCollection = await GetJsonAsync(stationUrl, cancellationToken);
        var stationFeatures = stationCollection.GetProperty("features");
        JsonElement observation = default;
        if (stationFeatures.GetArrayLength() > 0)
        {
            var stationId = stationFeatures[0].GetProperty("properties").GetProperty("stationIdentifier").GetString();
            if (!string.IsNullOrWhiteSpace(stationId))
            {
                try { observation = await GetJsonAsync($"https://api.weather.gov/stations/{stationId}/observations/latest", cancellationToken); }
                catch { }
            }
        }

        var alerts = await GetActiveAlertsAsync(latitude, longitude, cancellationToken);
        var useObservation = observation.ValueKind == JsonValueKind.Object &&
                              observation.TryGetProperty("properties", out var observationProperties);

        var temperature = useObservation
            ? observationProperties.GetProperty("temperature").GetProperty("value").GetDoubleOrNull()
            : period.GetProperty("temperature").GetDoubleOrNull();
        var condition = useObservation
            ? observationProperties.GetProperty("textDescription").GetString() ?? "Current conditions"
            : period.GetProperty("shortForecast").GetString() ?? "Forecast";
        var wind = useObservation
            ? BuildObservationWind(observationProperties)
            : $"{period.GetProperty("windSpeed").GetString() ?? ""} {period.GetProperty("windDirection").GetString() ?? ""}".Trim();
        var precipitation = period.TryGetProperty("probabilityOfPrecipitation", out var pop) && pop.TryGetProperty("value", out var popValue)
            ? popValue.GetDoubleOrNull()
            : null;

        return new WeatherData(
            city,
            temperature ?? double.NaN,
            condition,
            wind,
            precipitation,
            useObservation,
            alerts.Count,
            BuildAlertSummary(alerts));
    }

    public async Task<IReadOnlyList<WeatherAlert>> GetActiveAlertsAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        try
        {
            var root = await GetJsonAsync($"https://api.weather.gov/alerts/active?point={latitude:F4},{longitude:F4}", cancellationToken);
            var results = new List<WeatherAlert>();
            foreach (var feature in root.GetProperty("features").EnumerateArray())
            {
                var p = feature.GetProperty("properties");
                var id = p.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
                if (string.IsNullOrWhiteSpace(id)) continue;
                results.Add(new WeatherAlert(
                    id!,
                    p.TryGetProperty("event", out var evt) ? evt.GetString() ?? "Weather Alert" : "Weather Alert",
                    p.TryGetProperty("headline", out var headline) ? headline.GetString() ?? "" : "",
                    p.TryGetProperty("severity", out var severity) ? severity.GetString() ?? "Unknown" : "Unknown",
                    ParseDate(p, "effective"),
                    ParseDate(p, "expires")));
            }
            return results
                .GroupBy(a => a.Event + "|" + a.Headline, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(a => a.Severity, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<WeatherAlert>();
        }
    }

    private async Task<JsonElement> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }

    private static DateTimeOffset? ParseDate(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var value) && DateTimeOffset.TryParse(value.GetString(), out var parsed) ? parsed : null;

    private static string BuildObservationWind(JsonElement p)
    {
        var speed = p.GetProperty("windSpeed").GetProperty("value").GetDoubleOrNull();
        var direction = p.GetProperty("windDirection").GetProperty("value").GetDoubleOrNull();
        if (!speed.HasValue) return "Calm";
        return direction.HasValue ? $"{speed.Value:0} km/h @ {direction.Value:0}°" : $"{speed.Value:0} km/h";
    }

    private static string BuildAlertSummary(IReadOnlyList<WeatherAlert> alerts)
    {
        if (alerts.Count == 0) return "No active NWS alerts";
        var names = alerts.Take(3).Select(a => a.Event).Distinct(StringComparer.OrdinalIgnoreCase);
        var summary = string.Join(", ", names);
        return alerts.Count > 3 ? $"{summary} + {alerts.Count - 3} more" : summary;
    }
}

internal static class JsonElementExtensions
{
    public static double? GetDoubleOrNull(this JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var value)) return value;
        return null;
    }
}
