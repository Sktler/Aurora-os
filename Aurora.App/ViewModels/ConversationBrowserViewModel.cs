using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aurora.App.Services;

namespace Aurora.App.ViewModels
{
    public class FolderOption
    {
        public int? Id { get; init; }
        public string Name { get; init; } = "";
    }

    /// <summary>Search/filter view over saved conversations plus folder management.</summary>
    public partial class ConversationBrowserViewModel : ObservableObject
    {
        public static string NoFolderLabel => Localizer.GetUiText("No folder");
        public static string AllFoldersLabel => Localizer.GetUiText("All folders");

        public ObservableCollection<ConversationSummary> Results { get; } = new();
        public ObservableCollection<FolderOption> FolderFilters { get; } = new();

        [ObservableProperty] private string _searchText = "";
        [ObservableProperty] private string _tagFilter = "";
        [ObservableProperty] private bool _favoritesOnly;
        [ObservableProperty] private FolderOption? _selectedFolderFilter;
        [ObservableProperty] private string _newFolderName = "";
        [ObservableProperty] private string _status = "";

        public ConversationBrowserViewModel()
        {
            Localizer.LanguageChanged += OnLanguageChanged;
            ReloadFolders();
            Refresh();
        }

        private void OnLanguageChanged(object? sender, System.EventArgs e)
        {
            ReloadFolders();
            Refresh();
        }

        partial void OnSearchTextChanged(string value) => Refresh();
        partial void OnTagFilterChanged(string value) => Refresh();
        partial void OnFavoritesOnlyChanged(bool value) => Refresh();
        partial void OnSelectedFolderFilterChanged(FolderOption? value) => Refresh();

        private void ReloadFolders()
        {
            var selectedId = SelectedFolderFilter?.Id;
            FolderFilters.Clear();
            FolderFilters.Add(new FolderOption { Id = null, Name = AllFoldersLabel });
            if (App.Memory != null)
                foreach (var f in App.Memory.LoadFolders())
                    FolderFilters.Add(new FolderOption { Id = f.Id, Name = f.Name });
            SelectedFolderFilter = FolderFilters.FirstOrDefault(f => f.Id == selectedId) ?? FolderFilters[0];
        }

        [RelayCommand]
        public void Refresh()
        {
            Results.Clear();
            if (App.Memory == null) return;
            var names = App.Memory.LoadFolders().ToDictionary(f => f.Id, f => f.Name);
            foreach (var c in App.Memory.SearchConversations(SearchText, FavoritesOnly, TagFilter, SelectedFolderFilter?.Id))
            {
                c.FolderName = c.FolderId.HasValue && names.TryGetValue(c.FolderId.Value, out var n) ? n : NoFolderLabel;
                Results.Add(c);
            }
        }

        [RelayCommand]
        private void CreateFolder()
        {
            if (App.Memory == null || string.IsNullOrWhiteSpace(NewFolderName)) return;
            App.Memory.CreateFolder(NewFolderName);
            NewFolderName = "";
            ReloadFolders();
            Status = Localizer.GetUiText("Folder created.");
        }

        [RelayCommand]
        private void DeleteFolder()
        {
            var id = SelectedFolderFilter?.Id;
            if (App.Memory == null || id == null) { Status = Localizer.GetUiText("Select a folder to delete."); return; }
            App.Memory.DeleteFolder(id.Value);
            ReloadFolders();
            Refresh();
            Status = Localizer.GetUiText("Folder deleted. Its conversations were kept.");
        }

        [RelayCommand]
        private void ToggleFavorite(ConversationSummary? conversation)
        {
            if (App.Memory == null || conversation == null) return;
            App.Memory.SetConversationFavorite(conversation.CompanionId, !conversation.IsFavorite);
            Refresh();
        }
    }
}
