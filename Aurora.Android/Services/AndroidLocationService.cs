using Android.Content;
using Android.Locations;

namespace Aurora.Android.Services;

public sealed class AndroidLocationService(Context context)
{
    private readonly LocationManager _locationManager =
        (LocationManager)context.GetSystemService(Context.LocationService)!;

    public bool IsLocationEnabled =>
        _locationManager.IsProviderEnabled(LocationManager.GpsProvider) ||
        _locationManager.IsProviderEnabled(LocationManager.NetworkProvider);

    public Task<(double Latitude, double Longitude)?> GetLastKnownLocationAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Location? best = null;
        foreach (var provider in new[]
        {
            LocationManager.GpsProvider,
            LocationManager.NetworkProvider,
            LocationManager.PassiveProvider
        })
        {
            try
            {
                var location = _locationManager.GetLastKnownLocation(provider);
                if (location != null && (best == null || location.Time > best.Time))
                    best = location;
            }
            catch (SecurityException)
            {
                return Task.FromResult<(double Latitude, double Longitude)?>(null);
            }
        }

        if (best == null)
            return Task.FromResult<(double Latitude, double Longitude)?>(null);

        return Task.FromResult<(double Latitude, double Longitude)?>((best.Latitude, best.Longitude));
    }
}
