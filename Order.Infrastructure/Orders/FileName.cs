using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Order.Core.Orders;

namespace Order.Infrastructure.Orders;

public class FileName : IFileName
{
    private static readonly Regex RemoveInvalidChars = new Regex(
        $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]",
        RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5",
        "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4",
        "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private const int MaxFileNameLength = 255;

    public string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Unknown";
        var sanitized = RemoveInvalidChars.Replace(name, "_");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && 
            ReservedNames.Contains(sanitized.ToUpperInvariant())) sanitized = $"_{sanitized}";
        if (sanitized.Length > MaxFileNameLength)sanitized = sanitized.Substring(0, MaxFileNameLength);
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized;
    }
}