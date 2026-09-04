using System;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace ZoeyOS.App.Services
{
    public sealed record UserLocation(double Latitude, double Longitude);

    /// <summary>Gets the device location through Windows location services after user consent.</summary>
    public sealed class LocationService
    {
        public async Task<UserLocation?> GetCurrentLocationAsync()
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
                return null;

            var locator = new Geolocator { DesiredAccuracyInMeters = 100 };
            var position = await locator.GetGeopositionAsync(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
            var point = position.Coordinate.Point.Position;
            return new UserLocation(point.Latitude, point.Longitude);
        }
    }
}
