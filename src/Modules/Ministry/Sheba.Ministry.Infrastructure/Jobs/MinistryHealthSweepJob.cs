using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Application.Commands.TestMinistryConnection;
using Sheba.Ministry.Domain.Interfaces;

namespace Sheba.Ministry.Infrastructure.Jobs;

/// <summary>
/// Scheduled sweep (Phase 2 roadmap: "ministry health dashboard") that exercises every active
/// ministry auth config's connectivity check on a timer, so the admin dashboard reflects current
/// health without an operator having to click "test connection" per config.
///
/// Drives the exact same <see cref="TestMinistryConnectionCommand"/>/handler the manual
/// "test connection" endpoint uses — one adapter-selection and health-recording path for both
/// triggers, per this repo's single-source-of-truth convention (see T-SRV-4).
/// </summary>
public sealed class MinistryHealthSweepJob(
    IMinistryRepository repository,
    IMediator mediator,
    ILogger<MinistryHealthSweepJob> logger)
{
    public async Task SweepAsync(CancellationToken ct)
    {
        var configs = await repository.GetAllActiveAuthConfigsAsync(ct);

        var succeeded = 0;
        var failed = 0;
        foreach (var config in configs)
        {
            try
            {
                var result = await mediator.Send(new TestMinistryConnectionCommand(config.Id), ct);
                if (result.Success) succeeded++; else failed++;
            }
            catch (Exception ex)
            {
                // One misconfigured/unreachable ministry must not stop the sweep for the rest.
                failed++;
                logger.LogError(ex,
                    "[MinistryHealthSweep] Test failed for AuthConfig {Id} ({Name})",
                    config.Id, config.Name);
            }
        }

        logger.LogInformation(
            "[MinistryHealthSweep] Swept {Total} auth config(s): {Succeeded} healthy, {Failed} unhealthy.",
            configs.Count, succeeded, failed);
    }
}
