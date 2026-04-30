using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel(IScreen screen) : base(screen)
    {
        UrlPathSegment = "settings";
    }
}
