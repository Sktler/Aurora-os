using System;
using System.Net;
using System.Collections.Generic;
using System.Text.Json;

namespace Aurora.App.Services;

/// <summary>Displays actionable failures without exposing response bodies, URLs, or credentials.</summary>
public static class ProviderErrors
{
    public static string Format(string provider, HttpStatusCode status)
    {
        var guidance = status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Check the API key and account permissions in Settings > AI engine.",
            HttpStatusCode.NotFound => "The selected model is unavailable. Choose an available model in Settings > AI engine.",
            HttpStatusCode.TooManyRequests => "The provider's rate or usage limit was reached. Check your quota and try again later.",
            HttpStatusCode.BadRequest => "The provider rejected this request. Check the selected model and its supported features.",
            _ when (int)status >= 500 => "The provider is temporarily unavailable. Try again later.",
            _ => "Check your provider settings and try again."
        };
        return Localizer.FormatUiText("{0} request failed (HTTP {1}). {2}", provider, (int)status, Localizer.GetUiText(guidance));
    }

    public static string Connection(string provider, Exception error) => Localizer.FormatUiText(
        error is OperationCanceledException
            ? "{0} request was cancelled or timed out. Check the connection and provider status before retrying."
            : "Could not reach {0}. Check your connection and provider settings, then try again.", provider);

    internal static bool IsMissingGeminiModel(HttpStatusCode status, string body)
    {
        if (status != HttpStatusCode.NotFound) return false;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var error = doc.RootElement.GetProperty("error");
            return error.GetProperty("status").GetString() == "NOT_FOUND" &&
                error.GetProperty("message").GetString()?.Contains("model", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException) { return false; }
    }
}
