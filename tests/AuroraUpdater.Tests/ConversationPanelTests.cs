using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Aurora.App;
using Aurora.App.Models;
using Aurora.App.Services;
using Aurora.App.ViewModels;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public sealed class ConversationPanelTests : IDisposable
{
    private readonly AppSettings? _previousSettings = App.Settings;
    private readonly MemoryStore? _previousMemory = GetMemory();
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.db");
    private readonly MemoryStore _store;

    public ConversationPanelTests()
    {
        App.Settings = new AppSettings { SpeakRepliesByDefault = false };
        _store = new MemoryStore(_dbPath);
        _store.Initialize();
        SetMemory(_store);
        foreach (var id in new[] { "a", "b" })
        {
            _store.SaveCompanion(new Companion { Id = id, Name = "Comp " + id, Role = "r", SystemPrompt = "s", AccentHex = "#fff" });
            _store.AppendMessage(new ChatMessage { CompanionId = id, Role = "user", Content = id == "a" ? "tax paperwork" : "dinner ideas", Timestamp = DateTime.UtcNow });
        }
    }

    public void Dispose()
    {
        App.Settings = _previousSettings ?? new AppSettings();
        SetMemory(_previousMemory);
        _store.Dispose();
    }

    [Fact]
    public void Browser_FiltersBySearchFavoritesAndFolder()
    {
        var vm = new ConversationBrowserViewModel();
        Assert.Equal(2, vm.Results.Count);

        vm.SearchText = "tax";
        Assert.Equal("a", Assert.Single(vm.Results).CompanionId);

        vm.SearchText = "";
        vm.Results.First(r => r.CompanionId == "b");
        vm.ToggleFavoriteCommand.Execute(vm.Results.First(r => r.CompanionId == "b"));
        vm.FavoritesOnly = true;
        Assert.Equal("b", Assert.Single(vm.Results).CompanionId);
        vm.FavoritesOnly = false;

        vm.NewFolderName = "Home";
        vm.CreateFolderCommand.Execute(null);
        Assert.Equal(string.Empty, vm.NewFolderName);
        var folder = vm.FolderFilters.Single(f => f.Name == "Home");
        _store.SetConversationFolder("a", folder.Id);
        vm.SelectedFolderFilter = folder;
        Assert.Equal("a", Assert.Single(vm.Results).CompanionId);
        Assert.Equal("Home", vm.Results[0].FolderName);
    }

    [Fact]
    public void Browser_DeleteFolder_KeepsConversationsVisible()
    {
        var vm = new ConversationBrowserViewModel();
        vm.NewFolderName = "Temp";
        vm.CreateFolderCommand.Execute(null);
        var folder = vm.FolderFilters.Single(f => f.Name == "Temp");
        _store.SetConversationFolder("a", folder.Id);

        vm.SelectedFolderFilter = folder;
        vm.DeleteFolderCommand.Execute(null);

        Assert.DoesNotContain(vm.FolderFilters, f => f.Name == "Temp");
        Assert.Null(vm.SelectedFolderFilter?.Id);
        Assert.Equal(2, vm.Results.Count);
    }

    [Fact]
    public void Browser_DeleteWithoutSelection_DoesNothing()
    {
        var vm = new ConversationBrowserViewModel();
        vm.DeleteFolderCommand.Execute(null);
        Assert.Equal(2, vm.Results.Count);
        Assert.NotEmpty(vm.Status);
    }

    [Fact]
    public void CompanionViewModel_PersistsFavoriteFolderAndTags()
    {
        var folderId = _store.CreateFolder("Work");
        var vm = new CompanionViewModel(new Companion { Id = "a", Name = "Comp a" });
        Assert.False(vm.IsFavorite);
        vm.RefreshFoldersCommand.Execute(null);

        vm.IsFavorite = true;
        vm.SelectedFolder = vm.FolderChoices.Single(f => f.Id == folderId);
        vm.TagsText = "x, X , y";
        vm.SaveTagsCommand.Execute(null);

        Assert.Equal("x, y", vm.TagsText);
        var reloaded = new CompanionViewModel(new Companion { Id = "a", Name = "Comp a" });
        Assert.True(reloaded.IsFavorite);
        Assert.Equal(folderId, reloaded.SelectedFolder?.Id);
        Assert.Equal("x, y", reloaded.TagsText);
    }

    [Fact]
    public void CompanionViewModel_DeletedFolderFallsBackToNoFolder()
    {
        var folderId = _store.CreateFolder("Gone");
        _store.SetConversationFolder("a", folderId);
        _store.DeleteFolder(folderId);

        var vm = new CompanionViewModel(new Companion { Id = "a", Name = "Comp a" });
        Assert.Null(vm.SelectedFolder?.Id);
        Assert.Equal(ConversationBrowserViewModel.NoFolderLabel, vm.SelectedFolder?.Name);
    }

    [Theory]
    [InlineData("MemoryWindow.xaml", new[] { "SearchText", "TagFilter", "FolderFilters", "SelectedFolderFilter", "FavoritesOnly", "NewFolderName", "CreateFolderCommand", "DeleteFolderCommand", "ToggleFavoriteCommand", "Results" })]
    [InlineData("ChatWindow.xaml", new[] { "IsFavorite", "FolderChoices", "SelectedFolder", "TagsText", "SaveTagsCommand" })]
    public void Xaml_BindsToExistingViewModelMembers(string file, string[] members)
    {
        var xaml = File.ReadAllText(FindApp("Views", file));
        var vmTypes = new[] { typeof(ConversationBrowserViewModel), typeof(CompanionViewModel) };
        foreach (var member in members)
        {
            Assert.Contains("{Binding " + member, xaml.Replace("{Binding DataContext." + member, "{Binding " + member));
            Assert.True(
                vmTypes.Any(t => t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance) != null),
                $"{member} is not a public property on the view models");
        }
    }

    private static string FindApp(string folder, string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Aurora.sln"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "Aurora.App", folder, file);
    }

    private static MemoryStore? GetMemory() =>
        (MemoryStore?)typeof(App).GetProperty("Memory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);

    private static void SetMemory(MemoryStore? value) =>
        typeof(App).GetProperty("Memory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetSetMethod(true)?.Invoke(null, new object?[] { value });
}
