using Android.App;
using Android.Content;
using Android.OS;

namespace Aurora.Android.Services;

public sealed class AndroidNotificationService(Context context)
{
    private const string ChannelId = "aurora-alerts";
    private readonly Context _context = context;

    public void Show(string title, string message)
    {
        var manager = (NotificationManager?)_context.GetSystemService(Context.NotificationService);
        if (manager == null) return;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Aurora Alerts", NotificationImportance.High));

        var intent = _context.PackageManager?.GetLaunchIntentForPackage(_context.PackageName!);
        var pendingFlags = PendingIntentFlags.UpdateCurrent | (Build.VERSION.SdkInt >= BuildVersionCodes.M ? PendingIntentFlags.Immutable : 0);
        var pendingIntent = intent == null ? null : PendingIntent.GetActivity(_context, 0, intent, pendingFlags);

        var builder = new Notification.Builder(_context, Build.VERSION.SdkInt >= BuildVersionCodes.O ? ChannelId : string.Empty)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetAutoCancel(true);

        if (pendingIntent != null) builder.SetContentIntent(pendingIntent);
        manager.Notify(1001, builder.Build());
    }
}
