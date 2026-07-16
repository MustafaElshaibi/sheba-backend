using System.Text.RegularExpressions;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Shared.Kernel.ValueObjects;

/// <summary>
/// Yemeni National Identity Number.
/// Format: exactly 10 numeric digits (civil registry standard).
/// </summary>
public sealed record NationalId
{
    public string Value { get; }

    private static readonly Regex Pattern = new(@"^\d{10}$", RegexOptions.Compiled);

    private NationalId(string value) => Value = value;

    public static NationalId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("National ID cannot be empty.");

        var trimmed = value.Trim();

        if (!Pattern.IsMatch(trimmed))
            throw new DomainException($"'{trimmed}' is not a valid National ID (must be exactly 10 digits).");

        return new NationalId(trimmed);
    }

    public override string ToString() => Value;

    public static implicit operator string(NationalId nid) => nid.Value;
}
