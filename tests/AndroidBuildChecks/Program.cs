using System;
using System.IO;

var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var workflow = File.ReadAllText(Path.Combine(repo, ".github", "workflows", "android-build.yml"));
var sdkSetup = "android-actions/setup-android@v3";
var sdkPackages = "sdkmanager \"platform-tools\" \"platforms;android-35\" \"build-tools;35.0.0\"";
var workloadInstall = "dotnet workload install android";
var projectRestore = "dotnet restore Aurora.Android/Aurora.Android.csproj";
var sdkSetupIndex = workflow.IndexOf(sdkSetup, StringComparison.Ordinal);
var sdkPackagesIndex = workflow.IndexOf(sdkPackages, StringComparison.Ordinal);
var workloadIndex = workflow.IndexOf(workloadInstall, StringComparison.Ordinal);
var projectIndex = workflow.IndexOf(projectRestore, StringComparison.Ordinal);
var failures = 0;

Check(sdkSetupIndex >= 0, "Android workflow sets up the Android SDK");
Check(sdkPackagesIndex > sdkSetupIndex, "Android SDK packages are installed after SDK setup");
Check(workloadIndex > sdkPackagesIndex, "Android workload is installed after SDK setup");
Check(projectIndex > workloadIndex, "Android project restore runs after workload restore");

Console.WriteLine(failures == 0
    ? "PASS: Android build checks."
    : $"FAIL: {failures} Android build checks failed.");
return failures == 0 ? 0 : 1;

void Check(bool passed, string message)
{
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {message}");
    if (!passed) failures++;
}
