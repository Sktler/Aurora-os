using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Aurora.AndroidApp.Services;

public sealed class AndroidPermissionService(Activity activity)
{
    public const int RequestCode = 7001;

    public bool RequestLocationPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.M) return true;

        var permissions = new[]
        {
            global::Android.Manifest.Permission.AccessCoarseLocation,
            global::Android.Manifest.Permission.AccessFineLocation
        };
        var missing = permissions
            .Where(p => activity.CheckSelfPermission(p) != Permission.Granted)
            .ToArray();

        if (missing.Length == 0) return true;

        activity.RequestPermissions(missing, RequestCode);
        return false;
    }
}
