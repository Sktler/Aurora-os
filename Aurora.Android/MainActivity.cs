using Android.App;
using Android.Content.PM;
using Android.Hardware;
using Android.OS;
using Android.Widget;
using Aurora.Core;

namespace Aurora.Android;

[Activity(Label = "Aurora", MainLauncher = true, Exported = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public sealed class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var capabilities = new AndroidDeviceCapabilities(PackageManager, (SensorManager?)GetSystemService(SensorService));
        SetContentView(new TextView(this)
        {
            Text = $"Aurora Android\n\nUniversal Android build initialized.\n\nLocation: {YesNo(capabilities.HasLocation)}\nBluetooth: {YesNo(capabilities.HasBluetooth)}\nWi-Fi: {YesNo(capabilities.HasWifi)}\nCamera: {YesNo(capabilities.HasCamera)}\nMicrophone: {YesNo(capabilities.HasMicrophone)}\nGyroscope: {YesNo(capabilities.HasGyroscope)}\nAccelerometer: {YesNo(capabilities.HasAccelerometer)}\nNFC: {YesNo(capabilities.HasNfc)}\nBiometrics: {YesNo(capabilities.HasBiometrics)}",
            TextSize = 18
        });
    }

    private static string YesNo(bool value) => value ? "available" : "not available";
}

internal sealed class AndroidDeviceCapabilities : IDeviceCapabilities
{
    private readonly PackageManager _packages;
    private readonly SensorManager? _sensors;

    public AndroidDeviceCapabilities(PackageManager packages, SensorManager? sensors)
    {
        _packages = packages;
        _sensors = sensors;
    }

    public bool HasLocation => _packages.HasSystemFeature(PackageManager.FeatureLocation);
    public bool HasBluetooth => _packages.HasSystemFeature(PackageManager.FeatureBluetooth);
    public bool HasWifi => _packages.HasSystemFeature(PackageManager.FeatureWifi);
    public bool HasCamera => _packages.HasSystemFeature(PackageManager.FeatureCameraAny);
    public bool HasMicrophone => _packages.HasSystemFeature(PackageManager.FeatureMicrophone);
    public bool HasGyroscope => _sensors?.GetDefaultSensor(SensorType.Gyroscope) != null;
    public bool HasAccelerometer => _sensors?.GetDefaultSensor(SensorType.Accelerometer) != null;
    public bool HasNfc => _packages.HasSystemFeature(PackageManager.FeatureNfc);
    public bool HasBiometrics => Build.VERSION.SdkInt >= BuildVersionCodes.M && _packages.HasSystemFeature(PackageManager.FeatureFingerprint);
}
