using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class HomeViewModel : ViewModelBase
{
    public HomeViewModel(IScreen screen) : base(screen)
    {
        UrlPathSegment = "home";
    }
}
