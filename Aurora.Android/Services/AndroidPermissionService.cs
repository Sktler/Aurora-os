using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Aurora.Android.Services;

public sealed class AndroidPermissionService(Activity activity)
{
    public const int RequestCode = 7001;

    public void RequestRuntimePermissions()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.M) return;

        var permissions = new List<string>
        {
            global::Android.Manifest.Permission.AccessCoarseLocation,
            global::Android.Manifest.Permission.AccessFineLocation,
            global::Android.Manifest.Permission.Camera,
            global::Android.Manifest.Permission.RecordAudio
        };

        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            permissions.Add(global::Android.Manifest.Permission.BluetoothScan);
            permissions.Add(global::Android.Manifest.Permission.BluetoothConnect);
        }

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            permissions.Add(global::Android.Manifest.Permission.PostNotifications);

        var missing = permissions
            .Where(p => activity.CheckSelfPermission(p) != Permission.Granted)
            .Distinct()
            .ToArray();

        if (missing.Length > 0)
            activity.RequestPermissions(missing, RequestCode);
    }
}
