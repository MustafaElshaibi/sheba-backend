using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Document.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Document.Application.Commands.DeleteDocument;

public sealed class DeleteDocumentHandler(
    IDocumentRepository repository,
    IFileStorage storage,
    ILogger<DeleteDocumentHandler> logger
) : IRequestHandler<DeleteDocumentCommand, DeleteDocumentResponse>
{
    public async Task<DeleteDocumentResponse> Handle(DeleteDocumentCommand command, CancellationToken ct)
    {
        var doc = await repository.GetByIdAsync(command.DocumentId, ct)
            ?? throw new NotFoundException("Document", command.DocumentId);

        // Remove the object from MinIO, then soft-delete the metadata row
        await storage.DeleteAsync(doc.BucketName, doc.ObjectKey, ct);
        doc.MarkDeleted();
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[DeleteDocument] Deleted {Id}", command.DocumentId);
        return new DeleteDocumentResponse(true, "Document deleted.");
    }
}
