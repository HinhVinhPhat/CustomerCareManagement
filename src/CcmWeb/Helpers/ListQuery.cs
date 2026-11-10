using System.Globalization;

namespace CcmWeb.Helpers;

/// <summary>
/// Builds the query string of a list link. Every filter of M06 writes its state this way so the four
/// lists cannot drift apart; values equal to the default are left out, so a plain list keeps a plain
/// address (design.md 3.4).
/// </summary>
public sealed class ListQuery
{
    private readonly List<string> parameters = [];

    /// <summary>Adds <c>name=value</c> unless it is empty or equal to the default.</summary>
    public ListQuery Add(string name, string? value, string? defaultValue = null)
    {
        if (!string.IsNullOrEmpty(value) && value != defaultValue)
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value)}");
        }

        return this;
    }

    public ListQuery AddNumber(string name, int value, int? defaultValue = null)
    {
        if (value != defaultValue)
        {
            parameters.Add($"{name}={value.ToString(CultureInfo.InvariantCulture)}");
        }

        return this;
    }

    /// <summary>Adds a flag only when it is set; an unset flag never appears in the address.</summary>
    public ListQuery AddFlag(string name, bool value)
    {
        if (value)
        {
            // "true", not "1": the actions bind these to bool, and "1" is not a bool the binder knows.
            parameters.Add($"{name}=true");
        }

        return this;
    }

    public override string ToString() =>
        parameters.Count == 0 ? string.Empty : $"?{string.Join('&', parameters)}";
}
