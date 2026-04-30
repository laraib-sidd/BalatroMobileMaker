using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class ModEntry : ReactiveObject
{
    public string Name { get; }
    public string Path { get; }
    public long SizeBytes { get; }
    public bool IsRequired { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public string SizeDisplay => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        _ => $"{SizeBytes / (1024.0 * 1024.0):F1} MB"
    };

    public ModEntry(string name, string path, long sizeBytes, bool isRequired)
    {
        Name = name;
        Path = path;
        SizeBytes = sizeBytes;
        IsRequired = isRequired;
        _isSelected = true;
    }
}

public class ModManagerViewModel : ViewModelBase, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();
    public ObservableCollection<ModEntry> Mods { get; } = new();

    private string _summary = "";
    public string Summary { get => _summary; set => this.RaiseAndSetIfChanged(ref _summary, value); }

    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }
    public ReactiveCommand<Unit, Unit> DeselectAllCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public ModManagerViewModel(IScreen screen) : base(screen)
    {
        UrlPathSegment = "mods";

        SelectAllCommand = ReactiveCommand.Create(() =>
        {
            foreach (var mod in Mods) mod.IsSelected = true;
        });

        DeselectAllCommand = ReactiveCommand.Create(() =>
        {
            foreach (var mod in Mods)
                if (!mod.IsRequired) mod.IsSelected = false;
        });

        RefreshCommand = ReactiveCommand.Create(LoadMods);

        this.WhenActivated(disposables =>
        {
            LoadMods();

            Mods.ToObservableChangeSet()
                .AutoRefresh(m => m.IsSelected)
                .Subscribe(_ => UpdateSummary())
                .DisposeWith(disposables);
        });
    }

    private void LoadMods()
    {
        Mods.Clear();
        var balatroAppData = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Balatro", "Mods");

        if (!Directory.Exists(balatroAppData))
        {
            Summary = "Mods folder not found at " + balatroAppData;
            return;
        }

        foreach (var modDir in Directory.GetDirectories(balatroAppData))
        {
            var name = System.IO.Path.GetFileName(modDir);
            var size = GetDirectorySize(modDir);
            var isRequired = name.Equals("BalatroMobileCompat", StringComparison.OrdinalIgnoreCase);
            Mods.Add(new ModEntry(name, modDir, size, isRequired));
        }
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var selected = Mods.Count(m => m.IsSelected);
        Summary = $"{selected} of {Mods.Count} mods selected";
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch { return 0; }
    }
}
