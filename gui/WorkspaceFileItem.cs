using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace FishSyncClient.Gui;

public partial class WorkspaceFileItem(string path) : ObservableObject
{
    public string Path { get; } = path;
    [ObservableProperty] private string kind = "";
    [ObservableProperty] private string sizeText = "";
    [ObservableProperty] private IBrush background = Brushes.Transparent;

    private void Update(string state, long size)
    {
        Kind = state;
        SizeText = MainWindow.FormatSize(size);
        Background = BackgroundFor(state);
    }

    public static IBrush BackgroundFor(string state) => state switch
        {
            "추가" => Brush.Parse("#DDF4E4"),
            "삭제" => Brush.Parse("#FCE1E1"),
            "갱신" => Brush.Parse("#FFF0BB"),
            _ => Brushes.Transparent
        };

    public static void Reconcile(ObservableCollection<WorkspaceFileItem> items, List<LocalEntry> local, List<RemoteEntry> remote)
    {
        var changes = WorkspaceFiles.Compare(local, remote).ToDictionary(x => x.Path, WorkspaceFiles.Comparer);
        var desired = local.Where(x => !x.IsDirectory).ToDictionary(x => x.Path, x => x.Size, WorkspaceFiles.Comparer);
        foreach (var change in changes.Values.Where(x => x.Kind == "삭제"))
            desired.TryAdd(change.Path, change.Size);
        var existing = items.ToDictionary(x => x.Path, WorkspaceFiles.Comparer);
        for (var i = items.Count - 1; i >= 0; i--)
            if (!desired.ContainsKey(items[i].Path)) items.RemoveAt(i);
        var index = 0;
        foreach (var path in desired.Keys.OrderBy(x => x, WorkspaceFiles.Comparer))
        {
            if (!existing.TryGetValue(path, out var item))
            {
                item = new(path);
                items.Insert(index, item);
            }
            else if (items.IndexOf(item) != index) items.Move(items.IndexOf(item), index);
            item.Update(changes.GetValueOrDefault(path)?.Kind ?? "", desired[path]);
            index++;
        }
    }
}
