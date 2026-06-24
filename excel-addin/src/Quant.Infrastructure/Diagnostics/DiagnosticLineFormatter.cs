using System.Globalization;
using System.Text;
using Quant.Core.Diagnostics;

namespace Quant.Infrastructure.Diagnostics;

/// <summary>
/// Formats a <see cref="DiagnosticEvent"/> as a single, invariant-culture log line.
/// Carriage returns and line feeds in any field are escaped so that one event
/// always occupies exactly one line in the log file.
/// </summary>
internal static class DiagnosticLineFormatter
{
    internal static string FormatLine(in DiagnosticEvent diagnosticEvent)
    {
        var builder = new StringBuilder();
        builder.Append(diagnosticEvent.Timestamp.ToString("O", CultureInfo.InvariantCulture));
        builder.Append('\t');
        AppendEscaped(builder, diagnosticEvent.Source);
        builder.Append('\t');
        AppendEscaped(builder, diagnosticEvent.Message);
        builder.Append('\t');
        AppendEscaped(builder, diagnosticEvent.Detail);
        return builder.ToString();
    }

    private static void AppendEscaped(StringBuilder builder, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        foreach (var character in value)
        {
            switch (character)
            {
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }
    }
}
