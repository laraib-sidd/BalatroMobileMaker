namespace BalatroMobile.Configuration.Models;

public record PreFlightCheck
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Func<Task<CheckResult>> CheckFunction { get; init; } = () => Task.FromResult(CheckResult.Unknown);
    public bool IsRequired { get; init; } = true;
    public string? FixSuggestion { get; init; }
}

public enum CheckResult
{
    Pass,
    Fail,
    Warning,
    Unknown
}