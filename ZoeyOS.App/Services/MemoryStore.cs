using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using ZoeyOS.App.Models;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Local-first persistence. Everything lives in one SQLite file on your machine
    /// (%AppData%\ZoeyOS\zoeyos.db) - nothing here touches the network.
    /// </summary>
    public class MemoryStore : IDisposable
    {
        private readonly string _dbPath;
        private SqliteConnection? _conn;

        public MemoryStore(string dbPath) => _dbPath = dbPath;

        public void Initialize()
        {
            _conn = new SqliteConnection($"Data Source={_dbPath}");
            _conn.Open();

            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Companions (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    SystemPrompt TEXT NOT NULL,
                    AccentHex TEXT NOT NULL,
                    CanRunInBackground INTEGER NOT NULL DEFAULT 1,
                    ToolAccess TEXT NOT NULL DEFAULT 'General'
                );

                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanionId TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    Timestamp TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_Messages_CompanionId ON Messages(CompanionId);
            ";
            cmd.ExecuteNonQuery();

            // Migration: databases created before ToolAccess existed won't have the column -
            // CREATE TABLE IF NOT EXISTS only affects brand-new tables, so add it explicitly
            // for anyone upgrading from an earlier version.
            var checkCmd = _conn.CreateCommand();
            checkCmd.CommandText = "PRAGMA table_info(Companions);";
            var hasToolAccess = false;
            using (var reader = checkCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), "ToolAccess", StringComparison.OrdinalIgnoreCase))
                    {
                        hasToolAccess = true;
                        break;
                    }
                }
            }
            if (!hasToolAccess)
            {
                var alterCmd = _conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Companions ADD COLUMN ToolAccess TEXT NOT NULL DEFAULT 'General';";
                alterCmd.ExecuteNonQuery();

                // The new column defaults everyone to 'General'. That's correct for Aurora/Scout
                // (unchanged) and is actually a fix for Nova, which the old Role-text switch
                // silently gave zero tools to. But it would wrongly reset Sift and Home, which
                // relied on the old switch matching their exact Role text - backfill those two
                // explicitly so upgrading doesn't strip tools anyone already had.
                var backfillCmd = _conn.CreateCommand();
                backfillCmd.CommandText = @"
                    UPDATE Companions SET ToolAccess = 'HomeAutomation' WHERE Role = 'Home Automation';
                    UPDATE Companions SET ToolAccess = 'InboxDocuments' WHERE Role = 'Inbox & Documents';
                ";
                backfillCmd.ExecuteNonQuery();
            }
        }

        public void SaveCompanion(Companion c)
        {
            var cmd = _conn!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Companions (Id, Name, Role, SystemPrompt, AccentHex, CanRunInBackground, ToolAccess)
                VALUES ($id, $name, $role, $prompt, $accent, $bg, $tools)
                ON CONFLICT(Id) DO UPDATE SET
                    Name=$name, Role=$role, SystemPrompt=$prompt, AccentHex=$accent, CanRunInBackground=$bg, ToolAccess=$tools;
            ";
            cmd.Parameters.AddWithValue("$id", c.Id);
            cmd.Parameters.AddWithValue("$name", c.Name);
            cmd.Parameters.AddWithValue("$role", c.Role);
            cmd.Parameters.AddWithValue("$prompt", c.SystemPrompt);
            cmd.Parameters.AddWithValue("$accent", c.AccentHex);
            cmd.Parameters.AddWithValue("$bg", c.CanRunInBackground ? 1 : 0);
            cmd.Parameters.AddWithValue("$tools", c.ToolAccess.ToString());
            cmd.ExecuteNonQuery();
        }

        public List<Companion> LoadCompanions()
        {
            var result = new List<Companion>();
            var cmd = _conn!.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Role, SystemPrompt, AccentHex, CanRunInBackground, ToolAccess FROM Companions;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var toolAccess = Enum.TryParse<CompanionToolAccess>(reader.GetString(6), out var parsed)
                    ? parsed
                    : CompanionToolAccess.General;

                result.Add(new Companion
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Role = reader.GetString(2),
                    SystemPrompt = reader.GetString(3),
                    AccentHex = reader.GetString(4),
                    CanRunInBackground = reader.GetInt32(5) == 1,
                    ToolAccess = toolAccess
                });
            }
            return result;
        }

        public void AppendMessage(ChatMessage m)
        {
            var cmd = _conn!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Messages (CompanionId, Role, Content, Timestamp)
                VALUES ($cid, $role, $content, $ts);
            ";
            cmd.Parameters.AddWithValue("$cid", m.CompanionId);
            cmd.Parameters.AddWithValue("$role", m.Role);
            cmd.Parameters.AddWithValue("$content", m.Content);
            cmd.Parameters.AddWithValue("$ts", m.Timestamp.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public List<ChatMessage> LoadHistory(string companionId, int maxMessages = 40)
        {
            var result = new List<ChatMessage>();
            var cmd = _conn!.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, CompanionId, Role, Content, Timestamp FROM Messages
                WHERE CompanionId = $cid
                ORDER BY Id DESC
                LIMIT $max;
            ";
            cmd.Parameters.AddWithValue("$cid", companionId);
            cmd.Parameters.AddWithValue("$max", maxMessages);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new ChatMessage
                {
                    Id = reader.GetInt32(0),
                    CompanionId = reader.GetString(1),
                    Role = reader.GetString(2),
                    Content = reader.GetString(3),
                    Timestamp = DateTime.Parse(reader.GetString(4))
                });
            }
            result.Reverse(); // oldest first for sending to Claude
            return result;
        }

        /// <summary>Wipes one companion's history - used by the "Forget" privacy control.</summary>
        public void ClearHistory(string companionId)
        {
            var cmd = _conn!.CreateCommand();
            cmd.CommandText = "DELETE FROM Messages WHERE CompanionId = $cid;";
            cmd.Parameters.AddWithValue("$cid", companionId);
            cmd.ExecuteNonQuery();
        }

        public void Dispose() => _conn?.Dispose();
    }
}
