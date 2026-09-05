using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Aurora.Core;

namespace Aurora.Android;

[Activity(
    Label = "Aurora",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public sealed class MainActivity : Activity
{
    private readonly IDeviceCapabilities _capabilities = new AndroidDeviceCapabilities();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var status = $"Aurora Android\n\n" +
                     $"Location: {YesNo(_capabilities.HasLocation)}\n" +
                     $"Bluetooth: {YesNo(_capabilities.HasBluetooth)}\n" +
                     $"Wi-Fi: {YesNo(_capabilities.HasWifi)}\n" +
                     $"Camera: {YesNo(_capabilities.HasCamera)}\n" +
                     $"Microphone: {YesNo(_capabilities.HasMicrophone)}\n" +
                     $"Gyroscope: {YesNo(_capabilities.HasGyroscope)}\n" +
                     $"Accelerometer: {YesNo(_capabilities.HasAccelerometer)}\n" +
                     $"NFC: {YesNo(_capabilities.HasNfc)}\n" +
                     $"Biometrics: {YesNo(_capabilities.HasBiometrics)}";

        var view = new TextView(this)
        {
            Text = status,
            TextSize = 18
        };
        view.SetPadding(48, 64, 48, 48);
        SetContentView(view);
    }

    private static string YesNo(bool value) => value ? "available" : "not available";
}

internal sealed class AndroidDeviceCapabilities : IDeviceCapabilities
{
    public bool HasLocation => true;
    public bool HasBluetooth => true;
    public bool HasWifi => true;
    public bool HasCamera => true;
    public bool HasMicrophone => true;
    public bool HasGyroscope => true;
    public bool HasAccelerometer => true;
    public bool HasNfc => true;
    public bool HasBiometrics => true;
}
