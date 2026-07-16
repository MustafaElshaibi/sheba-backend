using MediatR;
using Microsoft.AspNetCore.Http;

namespace Sheba.Document.Application.Commands.UploadDocument;

public sealed record UploadDocumentCommand(
    Guid OwnerId,
    IFormFile File,
    string DocumentType = "GENERAL"
) : IRequest<UploadDocumentResponse>;

public sealed record UploadDocumentResponse(
    Guid DocumentId,
    string FileName,
    long SizeBytes,
    string Message);
