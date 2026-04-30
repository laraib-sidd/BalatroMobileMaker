using Avalonia.ReactiveUI;
using BalatroMobile.Gui.ViewModels;

namespace BalatroMobile.Gui.Views;

public partial class HomeView : ReactiveUserControl<HomeViewModel>
{
    public HomeView()
    {
        InitializeComponent();
    }
}
