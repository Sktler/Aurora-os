using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Weather client backed by the National Weather Service API.
    /// NWS is authoritative for U.S. forecasts, observations, and alerts.
    /// Place names are geocoded first because NWS /points requires latitude/longitude.
    /// </summary>
    public class WeatherClient
    {
        private const string NwsBaseUrl = "https://api.weather.gov";
        private readonly HttpClient _http;

        public WeatherClient()
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(NwsBaseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };
            _http.DefaultRequestHeaders.UserAgent.Clear();
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Aurora", "1.0"));
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/geo+json"));
        }

        public async Task<string> GetCurrentWeatherAsync(string place)
        {
            if (string.IsNullOrWhiteSpace(place))
                return "No location given.";

            try
            {
                var (lat, lon, resolvedName, state) = await ResolveLocationAsync(place);
                using var point = await GetJsonAsync($"/points/{lat.ToString(CultureInfo.InvariantCulture)},{lon.ToString(CultureInfo.InvariantCulture)}");
                var properties = point.RootElement.GetProperty("properties");

                var forecastUrl = properties.GetProperty("forecast").GetString();
                var hourlyUrl = properties.GetProperty("forecastHourly").GetString();
                var stationsUrl = properties.GetProperty("observationStations").GetString();

                if (string.IsNullOrWhiteSpace(forecastUrl) || string.IsNullOrWhiteSpace(hourlyUrl))
                    return $"NWS did not return forecast endpoints for {resolvedName}, {state}.";

                using var forecast = await GetJsonUrlAsync(forecastUrl);
                var currentPeriod = forecast.RootElement.GetProperty("properties").GetProperty("periods")[0];

                var condition = currentPeriod.GetProperty("shortForecast").GetString() ?? "Unknown conditions";
                var forecastTemp = currentPeriod.GetProperty("temperature").GetDouble();
                var tempUnit = currentPeriod.GetProperty("temperatureUnit").GetString() ?? "F";
                var windSpeed = currentPeriod.GetProperty("windSpeed").GetString() ?? "Unknown";
                var windDirection = currentPeriod.GetProperty("windDirection").GetString() ?? "";
                var precipitationChance = currentPeriod.GetProperty("probabilityOfPrecipitation").GetProperty("value");
                var pop = precipitationChance.ValueKind == JsonValueKind.Number ? precipitationChance.GetDouble() : (double?)null;

                var observed = await TryGetLatestObservationAsync(stationsUrl);
                var observedText = observed != null
                    ? $", observed {observed.Value.TemperatureF:0}°F"
                    : "";

                var popText = pop.HasValue ? $", precipitation chance {pop.Value:0}%" : "";
                var windText = string.IsNullOrWhiteSpace(windDirection) ? windSpeed : $"{windSpeed} {windDirection}";

                return $"Weather in {resolvedName}, {state}: {condition}, {forecastTemp:0}°{tempUnit}{observedText}, " +
                       $"wind {windText}{popText}. Source: National Weather Service.";
            }
            catch (HttpRequestException ex)
            {
                return $"Couldn't reach the National Weather Service: {ex.Message}";
            }
            catch (TaskCanceledException)
            {
                return "The National Weather Service request timed out.";
            }
            catch (Exception ex)
            {
                return $"Couldn't get NWS weather: {ex.Message}";
            }
        }

        private async Task<(double Lat, double Lon, string Name, string State)> ResolveLocationAsync(string place)
        {
            // NWS does not provide a general place-name geocoder. Keep Aurora's existing
            // free geocoding step, but make NWS the source of all weather data.
            var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(place)}&count=1&language=en&format=json";
            using var geo = await GetJsonUrlAsync(geoUrl);
            var results = geo.RootElement.TryGetProperty("results", out var resultArray) ? resultArray : default;

            if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
                throw new InvalidOperationException($"Couldn't find a location matching \"{place}\".");

            var first = results[0];
            var lat = first.GetProperty("latitude").GetDouble();
            var lon = first.GetProperty("longitude").GetDouble();
            var name = first.TryGetProperty("name", out var n) ? n.GetString() ?? place : place;
            var state = first.TryGetProperty("admin1", out var s) ? s.GetString() ?? "" : "";
            var countryCode = first.TryGetProperty("country_code", out var cc) ? cc.GetString() : null;

            if (!string.Equals(countryCode, "US", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The National Weather Service API provides U.S. weather data; the requested location is outside the U.S.");

            return (lat, lon, name, state);
        }

        private async Task<Observation?> TryGetLatestObservationAsync(string? stationsUrl)
        {
            if (string.IsNullOrWhiteSpace(stationsUrl))
                return null;

            try
            {
                using var stations = await GetJsonUrlAsync(stationsUrl);
                var features = stations.RootElement.GetProperty("features");
                if (features.ValueKind != JsonValueKind.Array || features.GetArrayLength() == 0)
                    return null;

                foreach (var station in features.EnumerateArray())
                {
                    var stationId = station.GetProperty("properties").GetProperty("stationIdentifier").GetString();
                    if (string.IsNullOrWhiteSpace(stationId))
                        continue;

                    using var observation = await GetJsonAsync($"/stations/{Uri.EscapeDataString(stationId)}/observations/latest");
                    var p = observation.RootElement.GetProperty("properties");
                    var temperature = p.GetProperty("temperature").GetProperty("value");
                    if (temperature.ValueKind != JsonValueKind.Number)
                        continue;

                    return new Observation(CelsiusToFahrenheit(temperature.GetDouble()));
                }
            }
            catch
            {
                // Forecast data remains useful if a nearby observation station is unavailable.
            }

            return null;
        }

        private async Task<JsonDocument> GetJsonAsync(string relativeUrl)
        {
            using var response = await _http.GetAsync(relativeUrl);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream);
        }

        private async Task<JsonDocument> GetJsonUrlAsync(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("NWS returned an empty API URL.");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Aurora", "1.0"));
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/geo+json"));

            using var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream);
        }

        private static double CelsiusToFahrenheit(double celsius) => celsius * 9.0 / 5.0 + 32.0;

        private readonly record struct Observation(double TemperatureF);
    }
}
