namespace Quant.Core.Diagnostics;

public readonly record struct DiagnosticStatus(bool IsEnabled, string Message, long DroppedEvents)
{
    public static DiagnosticStatus Disabled { get; } = new(false, "Disabled", 0);
}
