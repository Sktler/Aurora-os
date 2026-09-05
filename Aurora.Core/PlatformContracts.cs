namespace Aurora.Core;

public sealed record WeatherData(
    string Location,
    double TemperatureF,
    string Condition,
    string Wind,
    double? PrecipitationChance,
    bool IsObserved,
    int ActiveAlertCount,
    string ActiveAlertSummary);

public sealed record WeatherAlert(
    string Id,
    string Event,
    string Headline,
    string Severity,
    DateTimeOffset? Effective,
    DateTimeOffset? Expires,
    string Source = "NWS");

public interface IWeatherProvider
{
    Task<WeatherData> GetWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeatherAlert>> GetActiveAlertsAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

public interface IDeviceCapabilities
{
    bool HasLocation { get; }
    bool HasBluetooth { get; }
    bool HasWifi { get; }
    bool HasCamera { get; }
    bool HasMicrophone { get; }
    bool HasGyroscope { get; }
    bool HasAccelerometer { get; }
    bool HasNfc { get; }
    bool HasBiometrics { get; }
}

public interface IIntegration
{
    string Id { get; }
    string Name { get; }
    string Category { get; }
    IReadOnlyCollection<string> Capabilities { get; }
}

public interface IUpdateSource
{
    Task<UpdateInfo?> CheckForUpdateAsync(string currentVersion, CancellationToken cancellationToken = default);
}

public sealed record UpdateInfo(string Version, string DownloadUrl, string? ReleaseNotes = null);
