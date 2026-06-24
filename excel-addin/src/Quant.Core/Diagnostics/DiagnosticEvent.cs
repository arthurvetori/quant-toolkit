namespace Quant.Core.Diagnostics;

public readonly record struct DiagnosticEvent(
    DateTimeOffset Timestamp,
    string Source,
    string Message,
    string? Detail)
{
    public static DiagnosticEvent Error(string source, string message, string? detail) =>
        new(DateTimeOffset.UtcNow, source, message, detail);
}
