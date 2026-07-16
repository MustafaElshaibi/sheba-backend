using Sheba.Shared.Kernel.Entities;

namespace Sheba.Identity.Domain.Entities;

public sealed class IdentityRequestDocument : BaseEntity
{
    public Guid RequestId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public Guid DocumentServiceId { get; private set; }
    public DateTime UploadedAt { get; private set; } = DateTime.UtcNow;

    private IdentityRequestDocument() { }

    public static IdentityRequestDocument Create(
        Guid requestId,
        string documentType,
        Guid documentServiceId)
    {
        return new IdentityRequestDocument
        {
            RequestId = requestId,
            DocumentType = documentType,
            DocumentServiceId = documentServiceId
        };
    }
}