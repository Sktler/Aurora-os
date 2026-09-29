using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Aurora.App.Services;

public sealed record AppAdapterDefinition(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Permissions,
    bool Available);

public sealed record AppAdapterResult(
    bool Success,
    bool Preview,
    string Message,
    string? AdapterId = null,
    string? Action = null);

/// <summary>
/// Structured desktop-app adapters. Adapters are command/API based first and never
/// silently fall back to arbitrary shell commands. Missing adapters can fall back
/// to Aurora's normal permissioned file-open flow when a concrete target exists.
/// </summary>
public sealed class AppAdapterService
{
    private sealed record AdapterSpec(
        string Id,
        string Name,
        string Description,
        string[] Capabilities,
        string[] Permissions,
        Func<string?> ResolveExecutable);

    private readonly IReadOnlyList<AdapterSpec> _adapters;
    private readonly WindowsAutomationService _windows;

    public AppAdapterService(WindowsAutomationService? windows = null)
    {
        _windows = windows ?? App.WindowsAutomation;
        _adapters = new[]
        {
            new AdapterSpec("vscode", "Visual Studio Code",
                "Open files/folders and create files through the VS Code CLI.",
                new[] { "open_file", "open_folder", "new_file", "preview_write" },
                new[] { "Applications" }, ResolveVsCode),
            new AdapterSpec("word", "Microsoft Word",
                "Open documents and start a new Word document.",
                new[] { "open_document", "new_document" },
                new[] { "Applications" }, ResolveWord),
            new AdapterSpec("excel", "Microsoft Excel",
                "Open workbooks and start a new Excel workbook.",
                new[] { "open_workbook", "new_workbook" },
                new[] { "Applications" }, ResolveExcel),
            new AdapterSpec("powerpoint", "Microsoft PowerPoint",
                "Open presentations and start a new PowerPoint presentation.",
                new[] { "open_presentation", "new_presentation" },
                new[] { "Applications" }, ResolvePowerPoint),
            new AdapterSpec("notepad", "Windows Notepad",
                "Open a text file in the Windows text editor.",
                new[] { "open_file" },
                new[] { "Applications" }, ResolveNotepad)
        };
    }

    public IReadOnlyList<AppAdapterDefinition> GetAdapters() =>
        _adapters.Select(a => new AppAdapterDefinition(
            a.Id, a.Name, a.Description, a.Capabilities, a.Permissions,
            a.ResolveExecutable() != null)).ToList();

    public async Task<AppAdapterResult> ExecuteAsync(
        string adapterId, string action, string? target = null,
        string? content = null, bool confirm = false)
    {
        var adapter = _adapters.FirstOrDefault(a =>
            string.Equals(a.Id, adapterId, StringComparison.OrdinalIgnoreCase));
        if (adapter == null)
            return new(false, false, $"Unknown app adapter: {adapterId}.", adapterId, action);

        if (!_windows.ApplicationsEnabled)
            return new(false, false, "Application access is disabled in Aurora Settings.", adapterId, action);

        var normalizedAction = (action ?? string.Empty).Trim().ToLowerInvariant();
        var executable = adapter.ResolveExecutable();

        if (executable == null)
        {
            if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
            {
                if (!_windows.FilesEnabled)
                    return new(false, false,
                        $"{adapter.Name} is unavailable, and file access is disabled so Aurora cannot use the safe fallback.",
                        adapterId, action);
                try
                {
                    _windows.OpenPath(target);
                    return new(true, false,
                        $"{adapter.Name} is unavailable; opened the target with the Windows default application instead.",
                        adapterId, action);
                }
                catch (Exception ex)
                {
                    return new(false, false, $"Adapter unavailable and fallback failed: {ex.Message}", adapterId, action);
                }
            }
            return new(false, false,
                $"{adapter.Name} is not installed or its supported executable could not be found.",
                adapterId, action);
        }

        try
        {
            switch (adapterId.ToLowerInvariant(), normalizedAction)
            {
                case ("vscode", "open_file"):
                case ("vscode", "open_folder"):
                    return await LaunchAsync(executable,
                        new[] { "--reuse-window", RequireTarget(target) }, adapterId, action);

                case ("vscode", "new_file"):
                {
                    var path = RequireTarget(target);
                    var proposedContent = content ?? string.Empty;
                    if (!confirm)
                        return new(true, true, BuildWritePreview("VS Code", path, proposedContent), adapterId, action);

                    if (!_windows.FilesEnabled)
                        return new(false, false,
                            "File access is disabled; Aurora cannot create the requested file.", adapterId, action);

                    var fullPath = Path.GetFullPath(path);
                    var directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                        Directory.CreateDirectory(directory);
                    await File.WriteAllTextAsync(fullPath, proposedContent);
                    return await LaunchAsync(executable,
                        new[] { "--reuse-window", fullPath }, adapterId, action,
                        $"Created and opened {fullPath} in VS Code.");
                }

                case ("word", "open_document"):
                case ("excel", "open_workbook"):
                case ("powerpoint", "open_presentation"):
                case ("notepad", "open_file"):
                    return await LaunchAsync(executable,
                        new[] { RequireTarget(target) }, adapterId, action);

                case ("word", "new_document"):
                    return await LaunchAsync(executable, new[] { "/n" }, adapterId, action,
                        "Started a new Word document.");
                case ("excel", "new_workbook"):
                    return await LaunchAsync(executable, new[] { "/e" }, adapterId, action,
                        "Started a new Excel workbook.");
                case ("powerpoint", "new_presentation"):
                    return await LaunchAsync(executable, new[] { "/n" }, adapterId, action,
                        "Started a new PowerPoint presentation.");

                default:
                    return new(false, false,
                        $"Action '{action}' is not supported by the {adapter.Name} adapter.",
                        adapterId, action);
            }
        }
        catch (ArgumentException ex)
        {
            return new(false, false, ex.Message, adapterId, action);
        }
        catch (Exception ex)
        {
            return new(false, false,
                $"{adapter.Name} adapter failed safely: {ex.Message}", adapterId, action);
        }
    }

    public static string BuildWritePreview(string appName, string path, string content)
    {
        var preview = content.Length > 2000
            ? content[..2000] + Environment.NewLine + "[truncated]"
            : content;
        return $"Preview only — no file was changed. {appName} would create/update:{Environment.NewLine}{path}{Environment.NewLine}{Environment.NewLine}{preview}";
    }

    private static Task<AppAdapterResult> LaunchAsync(
        string executable, IReadOnlyList<string> arguments,
        string adapterId, string? action, string? successMessage = null)
    {
        var psi = new ProcessStartInfo { FileName = executable, UseShellExecute = true };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);
        Process.Start(psi);
        return Task.FromResult(new AppAdapterResult(
            true, false, successMessage ?? "Application action completed.", adapterId, action));
    }

    private static string RequireTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("A target file or folder is required for this action.");
        return target.Trim();
    }

    private static string? ResolveVsCode()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft VS Code", "Code.exe")
        };
        return FirstExisting(candidates) ?? FindOnPath("code.exe");
    }

    private static string? ResolveWord() => ResolveOfficeExecutable("WINWORD.EXE");
    private static string? ResolveExcel() => ResolveOfficeExecutable("EXCEL.EXE");
    private static string? ResolvePowerPoint() => ResolveOfficeExecutable("POWERPNT.EXE");

    private static string? ResolveOfficeExecutable(string executable)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }.Where(x => !string.IsNullOrWhiteSpace(x));

        var candidates = roots.SelectMany(root => new[]
        {
            Path.Combine(root, "Microsoft Office", "root", "Office16", executable),
            Path.Combine(root, "Microsoft Office", "Office16", executable),
            Path.Combine(root, "Microsoft Office", "root", "Office15", executable),
            Path.Combine(root, "Microsoft Office", "Office15", executable)
        });
        return FirstExisting(candidates);
    }

    private static string? ResolveNotepad() =>
        FirstExisting(new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe")
        }) ?? FindOnPath("notepad.exe");

    private static string? FirstExisting(IEnumerable<string> candidates) =>
        candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch { }
        }
        return null;
    }
}
