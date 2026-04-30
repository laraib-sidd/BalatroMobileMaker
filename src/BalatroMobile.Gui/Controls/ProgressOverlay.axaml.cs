using Avalonia;
using Avalonia.Controls;

namespace BalatroMobile.Gui.Controls;

public partial class ProgressOverlay : UserControl
{
    public static readonly StyledProperty<string> StepProperty =
        AvaloniaProperty.Register<ProgressOverlay, string>(nameof(Step), "Building...");

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ProgressOverlay, double>(nameof(Progress), 0);

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<ProgressOverlay, string?>(nameof(Detail));

    public static readonly StyledProperty<string?> ErrorProperty =
        AvaloniaProperty.Register<ProgressOverlay, string?>(nameof(Error));

    public string Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public string? Error { get => GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }

    public ProgressOverlay()
    {
        InitializeComponent();
        this.GetObservable(StepProperty).Subscribe(v => StepText.Text = v);
        this.GetObservable(ProgressProperty).Subscribe(v => ProgressBarControl.Value = v);
        this.GetObservable(DetailProperty).Subscribe(v => DetailText.Text = v);
        this.GetObservable(ErrorProperty).Subscribe(v =>
        {
            ErrorText.Text = v;
            ErrorBorder.IsVisible = !string.IsNullOrEmpty(v);
        });
    }
}
