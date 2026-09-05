using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Aurora.Android.Services;
using Aurora.Android.Updates;
using Aurora.Core;

namespace Aurora.Android;

[Activity(Label = "Aurora", Exported = false, ConfigurationChanges = Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.UiMode | Android.Content.PM.ConfigChanges.ScreenLayout | Android.Content.PM.ConfigChanges.SmallestScreenSize)]
public sealed class AuroraHomeActivity : Activity
{
    private LinearLayout _content = null!;
    private TextView _weatherText = null!;
    private TextView _alertText = null!;
    private TextView _statusText = null!;
    private AndroidLocationService _location = null!;
    private IWeatherProvider _weather = null!;
    private AndroidNotificationService _notifications = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.SetStatusBarColor(Color.ParseColor("#090B14"));
        Window?.SetNavigationBarColor(Color.ParseColor("#090B14"));
        _location = new AndroidLocationService(this);
        _weather = new NwsWeatherProvider();
        _notifications = new AndroidNotificationService(this);
        BuildUi();
        new AndroidPermissionService(this).RequestRuntimePermissions();
        _ = RefreshWeatherAsync();
        _ = CheckForUpdatesAsync();
    }

    private void BuildUi()
    {
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetBackgroundColor(Color.ParseColor("#090B14"));

        var header = new LinearLayout(this) { Orientation = Orientation.Vertical };
        header.SetPadding(28, 30, 28, 18);
        header.AddView(Label("AURORA", 30, true, Color.White));
        header.AddView(Label("Android companion", 15, false, Color.ParseColor("#A7B0C4")));
        root.AddView(header);

        var scroll = new ScrollView(this);
        _content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        _content.SetPadding(20, 4, 20, 32);
        scroll.AddView(_content);
        root.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));

        _statusText = Label("Starting Aurora…", 14, false, Color.ParseColor("#A7B0C4"));
        AddCard("SYSTEM", _statusText);

        var weatherCard = new LinearLayout(this) { Orientation = Orientation.Vertical };
        weatherCard.SetPadding(22, 20, 22, 20);
        weatherCard.SetBackgroundColor(Color.ParseColor("#121725"));
        weatherCard.AddView(Label("WEATHER", 13, true, Color.ParseColor("#8EA6FF")));
        _weatherText = Label("Loading NWS weather…", 23, true, Color.White);
        weatherCard.AddView(_weatherText);
        _alertText = Label("Checking active alerts…", 14, false, Color.ParseColor("#FFB86B"));
        weatherCard.AddView(_alertText);
        var refresh = new Button(this) { Text = "Refresh weather" };
        refresh.Click += async (_, _) => await RefreshWeatherAsync();
        weatherCard.AddView(refresh);
        AddCard(weatherCard);

        var capabilities = new AndroidDeviceCapabilities(PackageManager, (SensorManager?)GetSystemService(SensorService));
        AddCard("HARDWARE", Label(BuildCapabilities(capabilities), 15, false, Color.White));

        var actions = new LinearLayout(this) { Orientation = Orientation.Vertical };
        actions.AddView(Label("ACTIONS", 13, true, Color.ParseColor("#8EA6FF")));
        var location = new Button(this) { Text = "Open location settings" };
        location.Click += (_, _) => StartActivity(new Intent(Settings.ActionLocationSourceSettings));
        actions.AddView(location);
        var updates = new Button(this) { Text = "Check for Aurora updates" };
        updates.Click += async (_, _) => await CheckForUpdatesAsync(true);
        actions.AddView(updates);
        var testAlert = new Button(this) { Text = "Test Aurora notification" };
        testAlert.Click += (_, _) => _notifications.Show("Aurora", "Aurora Android notifications are working.");
        actions.AddView(testAlert);
        AddCard(actions);

        SetContentView(root);
    }

    private async Task RefreshWeatherAsync()
    {
        try
        {
            _statusText.Text = "Getting your Android location…";
            var location = await _location.GetLastKnownLocationAsync();
            if (location == null)
            {
                _weatherText.Text = "Location unavailable";
                _alertText.Text = "Allow location access and enable Location services, then refresh.";
                _statusText.Text = "Waiting for a location fix.";
                return;
            }

            var weather = await _weather.GetWeatherAsync(location.Value.Latitude, location.Value.Longitude);
            var temp = double.IsNaN(weather.TemperatureF) ? "--" : $"{weather.TemperatureF:0}°F";
            _weatherText.Text = $"{weather.Location}  •  {temp}\n{weather.Condition}\nWind: {weather.Wind}";
            _alertText.Text = weather.ActiveAlertCount == 0 ? "✓ No active NWS alerts" : $"⚠ {weather.ActiveAlertSummary}";
            _statusText.Text = $"NWS {(weather.IsObserved ? "observation" : "forecast")} • updated {DateTime.Now:t}";
            if (weather.ActiveAlertCount > 0) _notifications.Show("Aurora weather alert", weather.ActiveAlertSummary);
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Weather error: {ex.Message}";
        }
    }

    private async Task CheckForUpdatesAsync(bool showNoUpdate = false)
    {
        try
        {
            var source = new GitHubReleaseUpdateSource(new HttpClient());
            var update = await source.CheckForUpdateAsync("1.0.0");
            if (update == null)
            {
                if (showNoUpdate) Toast.MakeText(this, "Aurora is up to date.", ToastLength.Short)?.Show();
                return;
            }
            _notifications.Show("Aurora update available", $"Version {update.Version} is available.");
            var open = new Button(this) { Text = $"Download Aurora {update.Version}" };
            open.Click += (_, _) => StartActivity(new Intent(Intent.ActionView, Uri.Parse(update.DownloadUrl)));
            _content.AddView(open, 0);
        }
        catch
        {
            if (showNoUpdate) Toast.MakeText(this, "Update check unavailable.", ToastLength.Short)?.Show();
        }
    }

    private static string BuildCapabilities(AndroidDeviceCapabilities c) =>
        $"Location       {Mark(c.HasLocation)}\n" +
        $"Bluetooth      {Mark(c.HasBluetooth)}\n" +
        $"Wi-Fi          {Mark(c.HasWifi)}\n" +
        $"Camera         {Mark(c.HasCamera)}\n" +
        $"Microphone     {Mark(c.HasMicrophone)}\n" +
        $"Gyroscope      {Mark(c.HasGyroscope)}\n" +
        $"Accelerometer  {Mark(c.HasAccelerometer)}\n" +
        $"NFC            {Mark(c.HasNfc)}\n" +
        $"Biometrics     {Mark(c.HasBiometrics)}";

    private static string Mark(bool value) => value ? "✓" : "—";

    private void AddCard(string title, View child)
    {
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.SetPadding(22, 20, 22, 20);
        card.SetBackgroundColor(Color.ParseColor("#121725"));
        card.AddView(Label(title, 13, true, Color.ParseColor("#8EA6FF")));
        card.AddView(child);
        AddCard(card);
    }

    private void AddCard(View card)
    {
        _content.AddView(card, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = 18 });
    }

    private TextView Label(string text, float size, bool bold, Color color)
    {
        var label = new TextView(this) { Text = text, TextSize = size };
        label.SetTextColor(color);
        label.SetPadding(0, 4, 0, 10);
        if (bold) label.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        return label;
    }
}
