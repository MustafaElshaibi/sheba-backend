using System.Text.RegularExpressions;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Shared.Kernel.ValueObjects;

/// <summary>
/// Email address value object. Validates and stores in lowercase.
/// </summary>
public sealed record Email
{
    public string Value { get; }

    // RFC 5322 simplified — good enough for government registration
    private static readonly Regex Pattern = new(
        @"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private Email(string value) => Value = value;

    public static Email From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email address cannot be empty.");

        var trimmed = value.Trim();

        if (trimmed.Length > 320)
            throw new DomainException("Email address is too long (max 320 characters).");

        if (!Pattern.IsMatch(trimmed))
            throw new DomainException($"'{trimmed}' is not a valid email address.");

        return new Email(trimmed.ToLowerInvariant());
    }

    public string Domain => Value.Split('@')[1];

    public override string ToString() => Value;

    public static implicit operator string(Email email) => email.Value;
}
