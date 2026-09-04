using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    public sealed record WeatherSnapshot(string Location, double TemperatureF, string Condition, string Wind, double? PrecipitationChance, bool IsObserved);

    /// <summary>National Weather Service weather client. Weather data comes from api.weather.gov.</summary>
    public class WeatherClient
    {
        private const string NwsBaseUrl = "https://api.weather.gov";
        private readonly HttpClient _http;

        public WeatherClient()
        {
            _http = new HttpClient { BaseAddress = new Uri(NwsBaseUrl), Timeout = TimeSpan.FromSeconds(15) };
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Aurora", "1.0"));
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/geo+json"));
        }

        public async Task<WeatherSnapshot> GetCurrentWeatherAsync(double latitude, double longitude)
        {
            using var point = await GetJsonAsync($"/points/{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}");
            var p = point.RootElement.GetProperty("properties");
            var city = p.GetProperty("relativeLocation").GetProperty("properties").GetProperty("city").GetString() ?? "Current location";
            var state = p.GetProperty("relativeLocation").GetProperty("properties").GetProperty("state").GetString() ?? "";
            var location = string.IsNullOrWhiteSpace(state) ? city : $"{city}, {state}";
            var forecastUrl = p.GetProperty("forecast").GetString() ?? throw new InvalidOperationException("NWS did not return a forecast URL.");
            var stationsUrl = p.GetProperty("observationStations").GetString();

            using var forecast = await GetJsonUrlAsync(forecastUrl);
            var period = forecast.RootElement.GetProperty("properties").GetProperty("periods")[0];
            var forecastCondition = period.GetProperty("shortForecast").GetString() ?? "Unknown conditions";
            var forecastTemp = period.GetProperty("temperature").GetDouble();
            var forecastWind = period.GetProperty("windSpeed").GetString() ?? "Unknown";
            var forecastDirection = period.GetProperty("windDirection").GetString() ?? "";
            var popElement = period.GetProperty("probabilityOfPrecipitation").GetProperty("value");
            var pop = popElement.ValueKind == JsonValueKind.Number ? popElement.GetDouble() : (double?)null;

            var observation = await TryGetLatestObservationAsync(stationsUrl);
            if (observation.HasValue)
            {
                var wind = string.IsNullOrWhiteSpace(observation.Value.WindDirection) ? observation.Value.WindSpeed : $"{observation.Value.WindSpeed} {observation.Value.WindDirection}";
                return new WeatherSnapshot(location, observation.Value.TemperatureF, string.IsNullOrWhiteSpace(observation.Value.Condition) ? forecastCondition : observation.Value.Condition, wind, pop, true);
            }

            var forecastWindText = string.IsNullOrWhiteSpace(forecastDirection) ? forecastWind : $"{forecastWind} {forecastDirection}";
            return new WeatherSnapshot(location, forecastTemp, forecastCondition, forecastWindText, pop, false);
        }

        public async Task<string> GetCurrentWeatherAsync(string place)
        {
            if (string.IsNullOrWhiteSpace(place)) return "No location given.";
            try
            {
                var (lat, lon) = await ResolveLocationAsync(place);
                var weather = await GetCurrentWeatherAsync(lat, lon);
                var pop = weather.PrecipitationChance.HasValue ? $", precipitation chance {weather.PrecipitationChance.Value:0}%" : "";
                var observed = weather.IsObserved ? ", current observation" : ", NWS forecast";
                return $"Weather in {weather.Location}: {weather.Condition}, {weather.TemperatureF:0}°F, wind {weather.Wind}{pop}{observed}. Source: National Weather Service.";
            }
            catch (HttpRequestException ex) { return $"Couldn't reach the National Weather Service: {ex.Message}"; }
            catch (TaskCanceledException) { return "The National Weather Service request timed out."; }
            catch (Exception ex) { return $"Couldn't get NWS weather: {ex.Message}"; }
        }

        private async Task<(double Lat, double Lon)> ResolveLocationAsync(string place)
        {
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(place)}&count=1&language=en&format=json";
            using var geo = await GetJsonUrlAsync(url);
            var results = geo.RootElement.TryGetProperty("results", out var array) ? array : default;
            if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) throw new InvalidOperationException($"Couldn't find a location matching \"{place}\".");
            var first = results[0];
            var country = first.TryGetProperty("country_code", out var cc) ? cc.GetString() : null;
            if (!string.Equals(country, "US", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The National Weather Service API provides U.S. weather data.");
            return (first.GetProperty("latitude").GetDouble(), first.GetProperty("longitude").GetDouble());
        }

        private async Task<Observation?> TryGetLatestObservationAsync(string? stationsUrl)
        {
            if (string.IsNullOrWhiteSpace(stationsUrl)) return null;
            try
            {
                using var stations = await GetJsonUrlAsync(stationsUrl);
                var features = stations.RootElement.GetProperty("features");
                foreach (var station in features.EnumerateArray())
                {
                    var id = station.GetProperty("properties").GetProperty("stationIdentifier").GetString();
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    using var doc = await GetJsonAsync($"/stations/{Uri.EscapeDataString(id)}/observations/latest");
                    var p = doc.RootElement.GetProperty("properties");
                    var temp = p.GetProperty("temperature").GetProperty("value");
                    if (temp.ValueKind != JsonValueKind.Number) continue;
                    var windSpeed = p.GetProperty("windSpeed").GetProperty("value");
                    var windDirection = p.GetProperty("windDirection").GetProperty("value");
                    var condition = p.GetProperty("textDescription").GetString() ?? "";
                    return new Observation(CelsiusToFahrenheit(temp.GetDouble()), condition, windSpeed.ValueKind == JsonValueKind.Number ? $"{windSpeed.GetDouble():0} m/s" : "Unknown", windDirection.ValueKind == JsonValueKind.Number ? $"{windDirection.GetDouble():0}°" : "");
                }
            }
            catch { }
            return null;
        }

        private async Task<JsonDocument> GetJsonAsync(string relativeUrl)
        {
            using var response = await _http.GetAsync(relativeUrl);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream);
        }

        private async Task<JsonDocument> GetJsonUrlAsync(string url)
        {
            using var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream);
        }

        private static double CelsiusToFahrenheit(double celsius) => celsius * 9.0 / 5.0 + 32.0;
        private readonly record struct Observation(double TemperatureF, string Condition, string WindSpeed, string WindDirection);
    }
}
