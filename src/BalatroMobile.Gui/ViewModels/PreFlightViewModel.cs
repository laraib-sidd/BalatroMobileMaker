using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class PreFlightViewModel : ViewModelBase
{
    public PreFlightViewModel(IScreen screen) : base(screen)
    {
        UrlPathSegment = "preflight";
    }
}
