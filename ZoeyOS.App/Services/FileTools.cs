using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Tool definitions + execution for Sift's document access. Deliberately scoped to
    /// exactly one folder the user picked explicitly (Settings.TrustedFolderPath) - never
    /// the whole file system. Every resolved path is re-checked to still be inside that
    /// folder before anything is read, to block "../.." style escapes.
    /// </summary>
    public static class FileTools
    {
        // Extensions we can actually read as text. Binary/structured formats (PDF, DOCX,
        // XLSX, images) need real parsing this pass doesn't include - see the honest
        // message returned for those instead of silently failing or returning garbage.
        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".markdown", ".csv", ".tsv", ".json", ".xml", ".log",
            ".yml", ".yaml", ".ini", ".cfg", ".conf", ".cs", ".py", ".js", ".ts",
            ".html", ".htm", ".css", ".sql", ".sh", ".bat", ".ps1"
        };

        private const int MaxReadBytes = 300_000; // ~300 KB - keeps a single file from blowing out the conversation

        /// <summary>Reads a file as text if its extension is one we support, honoring the same
        /// size cap and format allowlist everywhere a file gets read into a conversation -
        /// the trusted-folder tool and the manual attach button both call this.</summary>
        public static (bool Ok, string ContentOrError) TryReadAsText(string fullPath)
        {
            if (!File.Exists(fullPath)) return (false, "That file doesn't exist.");

            var extension = Path.GetExtension(fullPath);
            if (!TextExtensions.Contains(extension))
                return (false, $"\"{Path.GetFileName(fullPath)}\" is a {extension} file - that format isn't " +
                                "readable yet (only plain-text-style formats are supported so far: txt, md, csv, json, code files, etc.).");

            try
            {
                var info = new FileInfo(fullPath);
                if (info.Length > MaxReadBytes)
                    return (false, $"\"{info.Name}\" is {FormatSize(info.Length)}, too large to read in full " +
                                    $"(limit is {FormatSize(MaxReadBytes)}).");

                return (true, File.ReadAllText(fullPath));
            }
            catch (Exception ex)
            {
                return (false, $"Couldn't read the file: {ex.Message}");
            }
        }

        public static List<object> Definitions => new()
        {
            new
            {
                name = "list_documents",
                description = "Lists files in the user's designated documents folder (set up in Settings). " +
                               "Returns no results if no folder has been set up yet.",
                input_schema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "read_document",
                description = "Reads the text content of a file in the user's designated documents folder. " +
                               "Only plain-text-readable formats work (txt, md, csv, json, code files, etc.) - " +
                               "PDFs, Word docs, spreadsheets, and images aren't supported yet.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        file_name = new { type = "string", description = "Exact file name as returned by list_documents, e.g. \"notes.txt\"." }
                    },
                    required = new[] { "file_name" }
                }
            }
        };

        public static Task<string> ExecuteAsync(string toolName, JsonElement input) => toolName switch
        {
            "list_documents" => Task.FromResult(ListDocuments()),
            "read_document" => Task.FromResult(ReadDocument(input)),
            _ => Task.FromResult($"Unknown tool: {toolName}")
        };

        private static string ListDocuments()
        {
            var folder = App.Settings.TrustedFolderPath;
            if (string.IsNullOrWhiteSpace(folder))
                return "No documents folder has been set up yet - add one in Settings.";

            if (!Directory.Exists(folder))
                return $"The configured folder no longer exists: {folder}. Pick a new one in Settings.";

            try
            {
                var files = Directory.GetFiles(folder)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Take(200) // sane cap so a huge folder doesn't flood the conversation
                    .Select(f => $"- {f.Name} ({FormatSize(f.Length)}{(TextExtensions.Contains(f.Extension) ? "" : ", not readable yet")})")
                    .ToList();

                return files.Count == 0
                    ? $"The folder is empty: {folder}"
                    : $"Files in {folder}:\n" + string.Join("\n", files);
            }
            catch (Exception ex)
            {
                return $"Couldn't list the folder: {ex.Message}";
            }
        }

        private static string ReadDocument(JsonElement input)
        {
            var folder = App.Settings.TrustedFolderPath;
            if (string.IsNullOrWhiteSpace(folder))
                return "No documents folder has been set up yet - add one in Settings.";

            var fileName = input.TryGetProperty("file_name", out var f) ? f.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(fileName))
                return "No file name given.";

            // Resolve and verify the final path is genuinely still inside the trusted
            // folder - blocks "../../elsewhere.txt" style escapes regardless of how the
            // model phrased the file name.
            string fullPath, folderFull;
            try
            {
                folderFull = Path.GetFullPath(folder);
                fullPath = Path.GetFullPath(Path.Combine(folderFull, fileName));
            }
            catch (Exception ex)
            {
                return $"Invalid file name: {ex.Message}";
            }

            if (!fullPath.StartsWith(folderFull, StringComparison.OrdinalIgnoreCase))
                return "That file is outside the designated folder, so it can't be read.";

            if (!File.Exists(fullPath))
                return $"No file named \"{fileName}\" in the designated folder.";

            var (ok, contentOrError) = TryReadAsText(fullPath);
            return ok ? $"Contents of {fileName}:\n\n{contentOrError}" : contentOrError;
        }

        private static string FormatSize(long bytes) =>
            bytes < 1024 ? $"{bytes} B" :
            bytes < 1024 * 1024 ? $"{bytes / 1024.0:0.#} KB" :
            $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}
