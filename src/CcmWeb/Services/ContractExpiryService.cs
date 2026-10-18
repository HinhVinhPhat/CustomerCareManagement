using CcmWeb.Models.EF;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// The database has no SQL Agent, so nothing moves contracts to Expired on its own. This hosted
/// service calls <c>sp_ExpireContracts</c> once at start-up and then once a day.
/// </summary>
public sealed class ContractExpiryService(
    IServiceScopeFactory scopeFactory,
    ILogger<ContractExpiryService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            await ExpireAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExpireAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var result = await db.Database
                .SqlQueryRaw<ExpiredContractsResult>("EXEC dbo.sp_ExpireContracts")
                .ToListAsync(cancellationToken);

            logger.LogInformation(
                "Contract expiry job finished: {Count} contract(s) moved to Expired",
                result.FirstOrDefault()?.UpdatedContracts ?? 0);
        }
        catch (Exception ex)
        {
            // A failure here must not stop the web application.
            logger.LogError(ex, "The contract expiry job failed");
        }
    }
}

public sealed class ExpiredContractsResult
{
    public int UpdatedContracts { get; set; }
}
