using BalatroMobile.Configuration.Models;

namespace BalatroMobile.Configuration.Services;

public interface IPreFlightCheckService
{
    Task<IEnumerable<PreFlightCheck>> GetAllChecksAsync();
    Task<PreFlightCheckResult> RunAllChecksAsync();
    Task<PreFlightCheckResult> RunCheckAsync(string checkName);
}

public record PreFlightCheckResult
{
    public bool AllPassed { get; init; }
    public IEnumerable<CheckResultDetail> Results { get; init; } = Array.Empty<CheckResultDetail>();
    public IEnumerable<string> Errors { get; init; } = Array.Empty<string>();
}

public record CheckResultDetail
{
    public string CheckName { get; init; } = string.Empty;
    public CheckResult Result { get; init; }
    public string? Message { get; init; }
    public string? FixSuggestion { get; init; }
}