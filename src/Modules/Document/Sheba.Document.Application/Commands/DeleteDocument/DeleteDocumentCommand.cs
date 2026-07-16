using MediatR;

namespace Sheba.Document.Application.Commands.DeleteDocument;

public sealed record DeleteDocumentCommand(Guid DocumentId) : IRequest<DeleteDocumentResponse>;

public sealed record DeleteDocumentResponse(bool Deleted, string Message);
