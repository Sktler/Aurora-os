using Android.Content.PM;
using Android.Hardware;
using Android.OS;
using Aurora.Core;

namespace Aurora.Android.Services;

public sealed class AndroidDeviceCapabilities : IDeviceCapabilities
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
