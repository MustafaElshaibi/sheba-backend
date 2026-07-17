using MediatR;

namespace Sheba.Document.Application.Commands.DeleteDocument;

/// <summary>Ownership is enforced in the handler (BR-DO-1) — a citizen can only delete their own document.</summary>
public sealed record DeleteDocumentCommand(Guid DocumentId, Guid ActorId, bool IsAdmin) : IRequest<DeleteDocumentResponse>;

public sealed record DeleteDocumentResponse(bool Deleted, string Message);
