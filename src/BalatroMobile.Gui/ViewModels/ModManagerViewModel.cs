using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class ModManagerViewModel : ViewModelBase
{
    public ModManagerViewModel(IScreen screen) : base(screen)
    {
        UrlPathSegment = "mods";
    }
}
