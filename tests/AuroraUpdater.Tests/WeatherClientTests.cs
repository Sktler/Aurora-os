using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class WeatherClientTests
{
    [Fact]
    public async Task Current_weather_includes_point_and_nationwide_alerts_without_duplicates()
    {
        var handler = new ScriptedHttpMessageHandler(new Dictionary<string, string>
        {
            ["https://api.weather.gov/points/39,-77"] = """
                {
                  "properties": {
                    "relativeLocation": {
                      "properties": {
                        "city": "Washington",
                        "state": "DC"
                      }
                    },
                    "forecast": "https://api.weather.gov/gridpoints/LWX/97,71/forecast",
                    "observationStations": null
                  }
                }
                """,
            ["https://api.weather.gov/gridpoints/LWX/97,71/forecast"] = """
                {
                  "properties": {
                    "periods": [
                      {
                        "shortForecast": "Sunny",
                        "temperature": 72,
                        "windSpeed": "5 mph",
                        "windDirection": "NW",
                        "probabilityOfPrecipitation": {
                          "value": null
                        }
                      }
                    ]
                  }
                }
                """,
            ["https://api.weather.gov/alerts/active?point=39,-77"] = """
                {
                  "features": [
                    {
                      "id": "local-alert",
                      "properties": {
                        "event": "Tornado Warning",
                        "headline": "Tornado Warning for Test County",
                        "severity": "Severe",
                        "areaDesc": "Test County",
                        "description": "Take shelter now.",
                        "instruction": "Move to an interior room."
                      }
                    }
                  ]
                }
                """,
            ["https://api.weather.gov/alerts/active?area=US"] = """
                {
                  "features": [
                    {
                      "id": "local-alert",
                      "properties": {
                        "event": "Tornado Warning",
                        "headline": "Tornado Warning for Test County",
                        "severity": "Severe",
                        "areaDesc": "Test County",
                        "description": "Take shelter now.",
                        "instruction": "Move to an interior room."
                      }
                    },
                    {
                      "id": "national-alert",
                      "properties": {
                        "event": "Extreme Heat Warning",
                        "headline": "Extreme Heat Warning for Multiple States",
                        "severity": "Extreme",
                        "areaDesc": "Multiple States",
                        "description": "Dangerous heat is expected.",
                        "instruction": "Limit outdoor exposure."
                      }
                    }
                  ]
                }
                """
        });

        var client = new WeatherClient(new HttpClient(handler));

        var snapshot = await client.GetCurrentWeatherAsync(39, -77);

        Assert.Equal("Washington, DC", snapshot.Location);
        Assert.Equal(2, snapshot.ActiveAlertCount);
        Assert.Contains("Tornado Warning", snapshot.ActiveAlertSummary);
        Assert.Contains("Extreme Heat Warning", snapshot.ActiveAlertSummary);
        Assert.DoesNotContain("plus 0 more", snapshot.ActiveAlertSummary);
    }

    private sealed class ScriptedHttpMessageHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, string> _responses;

        public ScriptedHttpMessageHandler(IReadOnlyDictionary<string, string> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri?.ToString() ?? string.Empty;
            if (!_responses.TryGetValue(requestUri, out var json))
                throw new InvalidOperationException($"Unexpected request: {requestUri}");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
