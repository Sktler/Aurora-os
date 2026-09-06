using Android.App;
using Android.Content;
using Android.OS;

namespace Aurora.AndroidApp;

[Activity(Label = "Aurora", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        StartActivity(new Intent(this, typeof(AuroraHomeActivity)));
        Finish();
    }
}
