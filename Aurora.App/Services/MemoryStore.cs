using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Aurora.App.Models;

namespace Aurora.App.Services
{
    public class ConversationFolder
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class ConversationSummary
    {
        public string CompanionId { get; set; } = "";
        public string Title { get; set; } = "";
        public bool IsFavorite { get; set; }
        public List<string> Tags { get; set; } = new();
        public int? FolderId { get; set; }
        public string FolderName { get; set; } = "";
        public string TagsText => string.Join(", ", Tags);
        public string FavoriteGlyph => IsFavorite ? "★" : "☆";
    }

    /// <summary>
    /// Local-first persistence. Everything lives in one SQLite file on your machine
    /// (%AppData%\Aurora\aurora.db) - nothing here touches the network.
    /// </summary>
    public class MemoryStore : IDisposable
    {
        private readonly string _dbPath;
        private SqliteConnection? _conn;
        public string ActiveProfileId { get; private set; } = "primary";

        public MemoryStore(string dbPath) => _dbPath = dbPath;

        public void SetActiveProfile(string profileId) => ActiveProfileId = string.IsNullOrWhiteSpace(profileId) ? "primary" : profileId;

        public void Initialize()
        {
            _conn = new SqliteConnection($"Data Source={_dbPath}");
            _conn.Open();

            var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Companions (
                    Id TEXT PRIMARY KEY,
                    ProfileId TEXT NOT NULL DEFAULT 'primary',
                    Name TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    SystemPrompt TEXT NOT NULL,
                    AccentHex TEXT NOT NULL,
                    CanRunInBackground INTEGER NOT NULL DEFAULT 1,
                    ToolAccess TEXT NOT NULL DEFAULT 'General'
                );

                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProfileId TEXT NOT NULL DEFAULT 'primary',
                    CompanionId TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    Timestamp TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_Messages_CompanionId ON Messages(CompanionId);

                CREATE TABLE IF NOT EXISTS ConversationFolders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProfileId TEXT NOT NULL DEFAULT 'primary',
                    Name TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ConversationMeta (
                    ProfileId TEXT NOT NULL,
                    CompanionId TEXT NOT NULL,
                    Title TEXT NOT NULL DEFAULT '',
                    IsFavorite INTEGER NOT NULL DEFAULT 0,
                    Tags TEXT NOT NULL DEFAULT '',
                    FolderId INTEGER NULL,
                    PRIMARY KEY (ProfileId, CompanionId)
                );
            ";
            cmd.ExecuteNonQuery();

            // Profile migration for databases created before multi-user profiles existed.
            EnsureColumn("Companions", "ProfileId", "TEXT NOT NULL DEFAULT 'primary'");
            EnsureColumn("Messages", "ProfileId", "TEXT NOT NULL DEFAULT 'primary'");

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
                INSERT INTO Companions (Id, ProfileId, Name, Role, SystemPrompt, AccentHex, CanRunInBackground, ToolAccess)
                VALUES ($id, $profile, $name, $role, $prompt, $accent, $bg, $tools)
                ON CONFLICT(Id) DO UPDATE SET
                    ProfileId=$profile, Name=$name, Role=$role, SystemPrompt=$prompt, AccentHex=$accent, CanRunInBackground=$bg, ToolAccess=$tools;
            ";
            cmd.Parameters.AddWithValue("$id", c.Id);
            cmd.Parameters.AddWithValue("$profile", ActiveProfileId);
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
            cmd.CommandText = "SELECT Id, Name, Role, SystemPrompt, AccentHex, CanRunInBackground, ToolAccess FROM Companions WHERE ProfileId = $profile;";
            cmd.Parameters.AddWithValue("$profile", ActiveProfileId);
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
                INSERT INTO Messages (ProfileId, CompanionId, Role, Content, Timestamp)
                VALUES ($profile, $cid, $role, $content, $ts);
            ";
            cmd.Parameters.AddWithValue("$profile", ActiveProfileId);
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
                WHERE ProfileId = $profile AND CompanionId = $cid
                ORDER BY Id DESC
                LIMIT $max;
            ";
            cmd.Parameters.AddWithValue("$profile", ActiveProfileId);
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

        public void DeleteProfileData(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId)) return;

            var cmd = _conn!.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM Messages WHERE ProfileId = $profile;
                DELETE FROM Companions WHERE ProfileId = $profile;
                DELETE FROM ConversationMeta WHERE ProfileId = $profile;
                DELETE FROM ConversationFolders WHERE ProfileId = $profile;
            ";
            cmd.Parameters.AddWithValue("$profile", profileId);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Wipes one companion's history - used by the "Forget" privacy control.</summary>
        public void ClearHistory(string companionId)
        {
            var cmd = _conn!.CreateCommand();
            cmd.CommandText = "DELETE FROM Messages WHERE ProfileId = $profile AND CompanionId = $cid;";
            cmd.Parameters.AddWithValue("$profile", ActiveProfileId);
            cmd.Parameters.AddWithValue("$cid", companionId);
            cmd.ExecuteNonQuery();
        }

        // ---- Conversation organization: search, tags, folders, favorites ----
        // A conversation is one companion's saved thread; organization data lives beside
        // the messages so ClearHistory/Forget never has to be coupled to it.

        public int CreateFolder(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) throw new ArgumentException("Folder name is required.", nameof(name));
            var cmd = _conn!.CreateCommand();
            cmd.CommandText = "INSERT INTO ConversationFolders (ProfileId, Name) VALUES ($p, $n); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$p", ActiveProfileId);
            cmd.Parameters.AddWithValue("$n", name);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public List<ConversationFolder> LoadFolders()
        {
            var result = new List<ConversationFolder>();
            var cmd = _conn!.CreateCommand();
            cmd.CommandText = "SELECT Id, Name FROM ConversationFolders WHERE ProfileId = $p ORDER BY Name COLLATE NOCASE;";
            cmd.Parameters.AddWithValue("$p", ActiveProfileId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(new ConversationFolder { Id = reader.GetInt32(0), Name = reader.GetString(1) });
            return result;
        }

        /// <summary>Deletes only the folder; conversations inside it are moved back to "no folder".</summary>
        public void DeleteFolder(int folderId)
        {
            var cmd = _conn!.CreateCommand();
            cmd.CommandText = @"
                UPDATE ConversationMeta SET FolderId = NULL WHERE ProfileId = $p AND FolderId = $f;
                DELETE FROM ConversationFolders WHERE ProfileId = $p AND Id = $f;
            ";
            cmd.Parameters.AddWithValue("$p", ActiveProfileId);
            cmd.Parameters.AddWithValue("$f", folderId);
            cmd.ExecuteNonQuery();
        }

        public void SetConversationTitle(string companionId, string title)
            => UpsertMeta(companionId, "Title", (title ?? "").Trim());

        public void SetConversationFavorite(string companionId, bool favorite)
            => UpsertMeta(companionId, "IsFavorite", favorite ? 1 : 0);

        public void SetConversationTags(string companionId, IEnumerable<string> tags)
        {
            var clean = new List<string>();
            foreach (var t in tags ?? Array.Empty<string>())
            {
                var tag = (t ?? "").Trim().Replace(",", " ");
                if (tag.Length > 0 && !clean.Exists(x => string.Equals(x, tag, StringComparison.OrdinalIgnoreCase)))
                    clean.Add(tag);
            }
            UpsertMeta(companionId, "Tags", string.Join(",", clean));
        }

        public void SetConversationFolder(string companionId, int? folderId)
            => UpsertMeta(companionId, "FolderId", folderId.HasValue ? folderId.Value : DBNull.Value);

        private void UpsertMeta(string companionId, string column, object value)
        {
            var cmd = _conn!.CreateCommand();
            // Column names come only from the fixed callers above, never from user input.
            cmd.CommandText = $@"
                INSERT INTO ConversationMeta (ProfileId, CompanionId, {column}) VALUES ($p, $c, $v)
                ON CONFLICT(ProfileId, CompanionId) DO UPDATE SET {column} = $v;
            ";
            cmd.Parameters.AddWithValue("$p", ActiveProfileId);
            cmd.Parameters.AddWithValue("$c", companionId);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Lists conversations for the active profile. Text matches the title, tags, or any
        /// message body (case-insensitive); other filters are ANDed together.
        /// </summary>
        public List<ConversationSummary> SearchConversations(string? text = null, bool favoritesOnly = false, string? tag = null, int? folderId = null)
        {
            var cmd = _conn!.CreateCommand();
            var where = new List<string>();
            cmd.Parameters.AddWithValue("$p", ActiveProfileId);

            if (!string.IsNullOrWhiteSpace(text))
            {
                var like = "%" + text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                cmd.Parameters.AddWithValue("$q", like);
                where.Add(@"(COALESCE(m.Title,'') LIKE $q ESCAPE '\' OR COALESCE(m.Tags,'') LIKE $q ESCAPE '\'
                    OR c.Name LIKE $q ESCAPE '\'
                    OR EXISTS (SELECT 1 FROM Messages g WHERE g.ProfileId = $p AND g.CompanionId = c.Id AND g.Content LIKE $q ESCAPE '\'))");
            }
            if (favoritesOnly) where.Add("COALESCE(m.IsFavorite,0) = 1");
            if (!string.IsNullOrWhiteSpace(tag))
            {
                cmd.Parameters.AddWithValue("$tag", "," + tag.Trim() + ",");
                where.Add("instr(lower(',' || COALESCE(m.Tags,'') || ','), lower($tag)) > 0");
            }
            if (folderId.HasValue)
            {
                cmd.Parameters.AddWithValue("$f", folderId.Value);
                where.Add("m.FolderId = $f");
            }

            cmd.CommandText = @"
                SELECT c.Id, c.Name, COALESCE(m.Title,''), COALESCE(m.IsFavorite,0), COALESCE(m.Tags,''), m.FolderId
                FROM Companions c
                LEFT JOIN ConversationMeta m ON m.ProfileId = c.ProfileId AND m.CompanionId = c.Id
                WHERE c.ProfileId = $p
                  AND EXISTS (SELECT 1 FROM Messages x WHERE x.ProfileId = $p AND x.CompanionId = c.Id)"
                + (where.Count > 0 ? " AND " + string.Join(" AND ", where) : "")
                + " ORDER BY COALESCE(m.IsFavorite,0) DESC, c.Name COLLATE NOCASE;";

            var result = new List<ConversationSummary>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var title = reader.GetString(2);
                var tags = reader.GetString(4);
                result.Add(new ConversationSummary
                {
                    CompanionId = reader.GetString(0),
                    Title = title.Length > 0 ? title : reader.GetString(1),
                    IsFavorite = reader.GetInt32(3) == 1,
                    Tags = tags.Length == 0 ? new List<string>() : new List<string>(tags.Split(',')),
                    FolderId = reader.IsDBNull(5) ? null : reader.GetInt32(5)
                });
            }
            return result;
        }

        /// <summary>Organization metadata for one companion's conversation (works even before any messages exist).</summary>
        public ConversationSummary GetConversation(string companionId)
        {
            var cmd = _conn!.CreateCommand();
            cmd.CommandText = "SELECT Title, IsFavorite, Tags, FolderId FROM ConversationMeta WHERE ProfileId = $p AND CompanionId = $c;";
            cmd.Parameters.AddWithValue("$p", ActiveProfileId);
            cmd.Parameters.AddWithValue("$c", companionId);
            using var reader = cmd.ExecuteReader();
            var summary = new ConversationSummary { CompanionId = companionId };
            if (reader.Read())
            {
                summary.Title = reader.GetString(0);
                summary.IsFavorite = reader.GetInt32(1) == 1;
                var tags = reader.GetString(2);
                summary.Tags = tags.Length == 0 ? new List<string>() : new List<string>(tags.Split(','));
                summary.FolderId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
            }
            return summary;
        }

        private void EnsureColumn(string table, string column, string definition)
        {
            var check = _conn!.CreateCommand();
            check.CommandText = $"PRAGMA table_info({table});";
            var exists = false;
            using (var reader = check.ExecuteReader())
                while (reader.Read())
                    if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
            if (!exists)
            {
                var alter = _conn.CreateCommand();
                alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
                alter.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            var connection = _conn;
            _conn = null;

            if (connection is not null)
                connection.Dispose();

            // Microsoft.Data.Sqlite may retain native SQLite handles in its pool
            // after a managed connection is disposed. Reset deletes the database
            // while this process is still alive, so release those handles first.
            SqliteConnection.ClearAllPools();
        }
    }
}
