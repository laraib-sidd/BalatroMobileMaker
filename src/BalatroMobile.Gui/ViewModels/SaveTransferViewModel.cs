using ReactiveUI;

namespace BalatroMobile.Gui.ViewModels;

public class SaveTransferViewModel : ViewModelBase
{
    public SaveTransferViewModel(IScreen screen) : base(screen)
    {
        UrlPathSegment = "save-transfer";
    }
}
