using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class ViewModelBase : ReactiveObject, IRoutableViewModel
{
    public string? UrlPathSegment { get; protected set; }
    public IScreen HostScreen { get; }

    protected ViewModelBase(IScreen screen)
    {
        HostScreen = screen;
    }
}
