using System;
using System.IO;

var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var workflow = File.ReadAllText(Path.Combine(repo, ".github", "workflows", "windows-build.yml"));
var project = File.ReadAllText(Path.Combine(repo, "ZoeyOS.App", "ZoeyOS.App.csproj"));
var app = File.ReadAllText(Path.Combine(repo, "ZoeyOS.App", "App.xaml.cs"));
var metrics = File.ReadAllText(Path.Combine(repo, "ZoeyOS.App", "Services", "SystemMetricsService.cs"));
var permission = File.ReadAllText(Path.Combine(repo, "ZoeyOS.App", "Services", "WindowsPermissionService.cs"));
var failures = 0;

Check(workflow.Contains("os: [windows-2022, windows-2025]", StringComparison.Ordinal),
    "Windows compatibility matrix covers Windows Server 2022 and 2025");
Check(workflow.Contains("dotnet-version: '10.0.x'", StringComparison.Ordinal),
    "Windows workflow uses the project's .NET SDK");
Check(workflow.Contains("dotnet publish ZoeyOS.App/ZoeyOS.App.csproj", StringComparison.Ordinal),
    "Windows workflow publishes the application");
Check(workflow.Contains("Start-Process -FilePath $app -PassThru", StringComparison.Ordinal),
    "Windows workflow launches the published application");
Check(workflow.Contains("CloseMainWindow", StringComparison.Ordinal),
    "Windows workflow exercises graceful application shutdown");
Check(workflow.Contains("Upload Windows application", StringComparison.Ordinal),
    "Windows workflow uploads the published application");
Check(project.Contains("<TargetFramework>net10.0-windows10.0.17763.0</TargetFramework>", StringComparison.Ordinal),
    "Windows project targets the declared Windows framework");
Check(app.Contains("ShutdownMode = ShutdownMode.OnMainWindowClose", StringComparison.Ordinal),
    "Windows app shuts down when its main window closes");
Check(app.Contains("MainWindow = mainWindow", StringComparison.Ordinal),
    "Windows app assigns Application.MainWindow before showing the window");
Check(app.Contains("Metrics = new SystemMetricsService()", StringComparison.Ordinal),
    "Windows metrics initialize before the main window");
Check(app.Contains("Optional service initialization failed", StringComparison.Ordinal),
    "Optional service initialization failures are surfaced without blocking startup");
Check(metrics.Contains("return -1", StringComparison.Ordinal) &&
      metrics.Contains("Array.Empty<PerformanceCounter>()", StringComparison.Ordinal),
    "Unavailable Windows performance counters use an N/A sentinel");
Check(permission.Contains("PermissionResult.Denied", StringComparison.Ordinal) &&
      permission.Contains("PermissionResult.Unavailable", StringComparison.Ordinal),
    "Windows permission failures have explicit denied and unavailable outcomes");

Console.WriteLine(failures == 0
    ? "PASS: Windows build checks."
    : $"FAIL: {failures} Windows build checks failed.");
return failures == 0 ? 0 : 1;

void Check(bool passed, string message)
{
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {message}");
    if (!passed) failures++;
}
