using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Domain.Entities;

public sealed class AdminUser : BaseEntity
{
    public string EmployeeId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public AdminRole Role { get; private set; }
    public string? Department { get; private set; }
    public string Status { get; private set; } = "ACTIVE";
    public string PasswordHash { get; private set; } = string.Empty;
    public string? MfaSecret { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    private AdminUser() { }

    public static AdminUser Create(
        string employeeId,
        string email,
        string fullName,
        AdminRole role,
        string passwordHash,
        string? department = null)
    {
        var admin = new AdminUser
        {
            EmployeeId = employeeId,
            Email = email,
            FullName = fullName,
            Role = role,
            Department = department,
            PasswordHash = passwordHash
        };

        return admin;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        Touch();
    }

    public void SetMfaSecret(string encryptedSecret)
    {
        MfaSecret = encryptedSecret;
        Touch();
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        Touch();
    }

    public void Deactivate()
    {
        if (Status is "DEACTIVATED")
            throw new DomainException("Admin user is already deactivated.");

        Status = "DEACTIVATED";
        Touch();
    }

    public void Suspend()
    {
        if (Status is not "ACTIVE")
            throw new DomainException("Only active admin users can be suspended.");

        Status = "SUSPENDED";
        Touch();
    }

    public void Reactivate()
    {
        if (Status is "ACTIVE")
            throw new DomainException("Admin user is already active.");

        Status = "ACTIVE";
        Touch();
    }
}