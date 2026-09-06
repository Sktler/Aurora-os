using System;
using System.IO;

var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var workflow = File.ReadAllText(Path.Combine(repo, ".github", "workflows", "android-build.yml"));
var project = File.ReadAllText(Path.Combine(repo, "Aurora.Android", "Aurora.Android.csproj"));
var manifest = File.ReadAllText(Path.Combine(repo, "Aurora.Android", "AndroidManifest.xml"));
var sdkSetup = "android-actions/setup-android@v3";
var sdkPackages = "sdkmanager \"platform-tools\" \"platforms;android-35\" \"build-tools;35.0.0\"";
var workloadInstall = "dotnet workload install android";
var projectRestore = "dotnet restore Aurora.Android/Aurora.Android.csproj";
var projectPublish = "dotnet publish Aurora.Android/Aurora.Android.csproj --configuration Release --framework net10.0-android -p:AndroidPackageFormat=apk";
var artifactUpload = "path: Aurora.Android/bin/Release/net10.0-android/**/*.apk";
var releaseUpload = "uses: softprops/action-gh-release@v2";
var releaseCondition = "if: startsWith(github.ref, 'refs/tags/')";
var sdkSetupIndex = workflow.IndexOf(sdkSetup, StringComparison.Ordinal);
var sdkPackagesIndex = workflow.IndexOf(sdkPackages, StringComparison.Ordinal);
var workloadIndex = workflow.IndexOf(workloadInstall, StringComparison.Ordinal);
var projectIndex = workflow.IndexOf(projectRestore, StringComparison.Ordinal);
var publishIndex = workflow.IndexOf(projectPublish, StringComparison.Ordinal);
var artifactIndex = workflow.IndexOf(artifactUpload, StringComparison.Ordinal);
var releaseIndex = workflow.IndexOf(releaseUpload, StringComparison.Ordinal);
var releaseConditionIndex = workflow.IndexOf(releaseCondition, StringComparison.Ordinal);
var failures = 0;

Check(sdkSetupIndex >= 0, "Android workflow sets up the Android SDK");
Check(sdkPackagesIndex > sdkSetupIndex, "Android SDK packages are installed after SDK setup");
Check(workloadIndex > sdkPackagesIndex, "Android workload is installed after SDK setup");
Check(projectIndex > workloadIndex, "Android project restore runs after workload restore");
Check(publishIndex > projectIndex, "Android project is published as an APK");
Check(artifactIndex > publishIndex, "Published APK is uploaded as a workflow artifact");
Check(releaseConditionIndex > artifactIndex, "Release upload is limited to tag builds");
Check(releaseIndex > releaseConditionIndex, "Tagged APK is uploaded to the GitHub Release");
Check(workflow.Contains("tags: [ 'v*' ]", StringComparison.Ordinal),
    "Android workflow runs for version tags");
Check(workflow.Contains("contents: write", StringComparison.Ordinal),
    "Android workflow can upload release assets");
Check(project.Contains("<TargetFramework>net10.0-android</TargetFramework>", StringComparison.Ordinal),
    "Android project targets .NET Android");
Check(project.Contains("<AndroidPackageFormat>apk</AndroidPackageFormat>", StringComparison.Ordinal),
    "Android project declares APK packaging");
foreach (var feature in new[]
{
    "android.hardware.camera.any",
    "android.hardware.microphone",
    "android.hardware.bluetooth",
    "android.hardware.wifi",
    "android.hardware.nfc",
    "android.hardware.location",
    "android.hardware.sensor.gyroscope",
    "android.hardware.sensor.accelerometer",
    "android.hardware.biometrics.face",
    "android.hardware.fingerprint"
})
{
    Check(manifest.Contains($"<uses-feature android:name=\"{feature}\" android:required=\"false\" />",
        StringComparison.Ordinal),
        $"{feature} is optional so devices without it can install Aurora");
}
Check(workflow.Contains("api-level: [23, 29, 35]", StringComparison.Ordinal),
    "Android compatibility matrix covers API 23, 29, and 35");
Check(workflow.Contains("adb install", StringComparison.Ordinal),
    "Android compatibility job installs the release APK");
Check(workflow.Contains("adb shell monkey -p com.sktler.aurora", StringComparison.Ordinal),
    "Android compatibility job launches the installed APK");
Check(workflow.Contains("adb shell pidof com.sktler.aurora", StringComparison.Ordinal),
    "Android compatibility job verifies the app process stays alive");
Check(workflow.Contains("adb shell input keyevent 3", StringComparison.Ordinal),
    "Android compatibility job covers background and foreground transitions");
Check(workflow.Contains("adb shell svc wifi disable", StringComparison.Ordinal),
    "Android compatibility job covers unavailable network services");

Console.WriteLine(failures == 0
    ? "PASS: Android build checks."
    : $"FAIL: {failures} Android build checks failed.");
return failures == 0 ? 0 : 1;

void Check(bool passed, string message)
{
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {message}");
    if (!passed) failures++;
}
