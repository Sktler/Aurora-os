using System;
using System.IO;
using System.Linq;
using Aurora.App.Models;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public class ConversationOrganizationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aurora-conv-" + Guid.NewGuid().ToString("N"));
    private readonly MemoryStore _store;

    public ConversationOrganizationTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new MemoryStore(Path.Combine(_dir, "t.db"));
        _store.Initialize();
        foreach (var id in new[] { "a", "b" })
        {
            _store.SaveCompanion(new Companion { Id = id, Name = "Comp " + id, Role = "r", SystemPrompt = "s", AccentHex = "#fff" });
            _store.AppendMessage(new ChatMessage { CompanionId = id, Role = "user", Content = id == "a" ? "plan the 100% budget" : "weather today", Timestamp = DateTime.UtcNow });
        }
    }

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Search_MatchesMessageTextTitleAndEscapesWildcards()
    {
        Assert.Equal("a", Assert.Single(_store.SearchConversations("budget")).CompanionId);
        Assert.Equal("a", Assert.Single(_store.SearchConversations("100%")).CompanionId);
        Assert.Empty(_store.SearchConversations("%zzz"));
        _store.SetConversationTitle("b", "Holiday trip");
        Assert.Equal("b", Assert.Single(_store.SearchConversations("holiday")).CompanionId);
    }

    [Fact]
    public void TagsFavoritesAndFolders_PersistAndFilter()
    {
        _store.SetConversationTags("a", new[] { "work", "Work", " ideas " });
        _store.SetConversationFavorite("b", true);
        var folder = _store.CreateFolder("Projects");
        _store.SetConversationFolder("a", folder);

        Assert.Equal("a", Assert.Single(_store.SearchConversations(tag: "WORK")).CompanionId);
        Assert.Empty(_store.SearchConversations(tag: "wor"));
        Assert.Equal("b", Assert.Single(_store.SearchConversations(favoritesOnly: true)).CompanionId);
        Assert.Equal("a", Assert.Single(_store.SearchConversations(folderId: folder)).CompanionId);
        Assert.Equal(new[] { "work", "ideas" }, _store.SearchConversations(tag: "work")[0].Tags);
    }

    [Fact]
    public void DeletingFolder_KeepsConversations()
    {
        var folder = _store.CreateFolder("Temp");
        _store.SetConversationFolder("a", folder);
        _store.DeleteFolder(folder);

        Assert.Empty(_store.LoadFolders());
        var all = _store.SearchConversations();
        Assert.Equal(2, all.Count);
        Assert.All(all, c => Assert.Null(c.FolderId));
        Assert.Single(_store.LoadHistory("a"));
    }
}
