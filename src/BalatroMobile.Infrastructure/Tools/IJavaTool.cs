namespace BalatroMobile.Infrastructure.Tools;

public interface IJavaTool
{
    Task<bool> ExecuteAsync(string arguments, string? workingDirectory = null);
    Task<string> ExecuteAndCaptureOutputAsync(string arguments, string? workingDirectory = null);
    Task<bool> IsAvailableAsync();
    string GetVersion();
}