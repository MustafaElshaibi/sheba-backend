using System.Text.RegularExpressions;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Shared.Kernel.ValueObjects;

/// <summary>
/// Yemeni mobile phone number.
/// Accepted formats: 07XXXXXXXX (local) or +9677XXXXXXXX (international).
/// Stored in normalised +967 international format.
/// </summary>
public sealed record PhoneNumber
{
    public string Value { get; }

    // Matches: 07XXXXXXXX  or  +9677XXXXXXXX  or  9677XXXXXXXX
    private static readonly Regex LocalPattern       = new(@"^0(7[0-9]{8})$",        RegexOptions.Compiled);
    private static readonly Regex InternationalPattern = new(@"^\+?967(7[0-9]{8})$", RegexOptions.Compiled);

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Phone number cannot be empty.");

        var trimmed = value.Trim().Replace(" ", "").Replace("-", "");

        var localMatch = LocalPattern.Match(trimmed);
        if (localMatch.Success)
            return new PhoneNumber("+967" + localMatch.Groups[1].Value);

        var intlMatch = InternationalPattern.Match(trimmed);
        if (intlMatch.Success)
            return new PhoneNumber("+967" + intlMatch.Groups[1].Value);

        throw new DomainException($"'{value}' is not a valid Yemeni phone number.");
    }

    /// <summary>Display format without country code (07XXXXXXXX).</summary>
    public string ToLocalFormat() => "0" + Value[4..];

    public override string ToString() => Value;

    public static implicit operator string(PhoneNumber phone) => phone.Value;
}
