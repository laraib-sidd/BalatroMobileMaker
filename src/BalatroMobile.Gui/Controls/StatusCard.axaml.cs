using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace BalatroMobile.Gui.Controls;

public partial class StatusCard : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<StatusCard, string>(nameof(Title), "Status");

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<StatusCard, string>(nameof(Value), "Unknown");

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<StatusCard, string?>(nameof(Detail));

    public static readonly StyledProperty<CardStatus> StatusProperty =
        AvaloniaProperty.Register<StatusCard, CardStatus>(nameof(Status), CardStatus.Unknown);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public CardStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public StatusCard()
    {
        InitializeComponent();
        this.GetObservable(TitleProperty).Subscribe(v => TitleText.Text = v);
        this.GetObservable(ValueProperty).Subscribe(v => ValueText.Text = v);
        this.GetObservable(DetailProperty).Subscribe(v =>
        {
            DetailText.Text = v;
            DetailText.IsVisible = !string.IsNullOrEmpty(v);
        });
        this.GetObservable(StatusProperty).Subscribe(UpdateStatusDot);
    }

    private void UpdateStatusDot(CardStatus status)
    {
        var color = status switch
        {
            CardStatus.Pass => Color.Parse("#4ecca3"),
            CardStatus.Warning => Color.Parse("#ffc857"),
            CardStatus.Fail => Color.Parse("#ff6b6b"),
            _ => Color.Parse("#8888aa")
        };
        StatusDot.Fill = new SolidColorBrush(color);
    }
}

public enum CardStatus
{
    Unknown,
    Pass,
    Warning,
    Fail
}
