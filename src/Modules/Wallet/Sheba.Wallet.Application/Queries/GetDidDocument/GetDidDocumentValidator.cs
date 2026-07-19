using FluentValidation;

namespace Sheba.Wallet.Application.Queries.GetDidDocument;

public sealed class GetDidDocumentValidator : AbstractValidator<GetDidDocumentQuery>
{
    public GetDidDocumentValidator()
    {
        RuleFor(x => x.Did)
            .NotEmpty()
            .Must(d => d.StartsWith("did:sheba:", StringComparison.Ordinal))
            .WithMessage("Only did:sheba:* identifiers are resolvable.");
    }
}
