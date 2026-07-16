using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Sheba.Api.Extensions;

/// <summary>
/// Extension method that runs EF Core migrations for all module DbContexts on startup.
/// This ensures the database schema is always up-to-date when the API starts — 
/// essential for Docker Compose deployments where the API starts alongside Postgres.
/// </summary>
public static class MigrationExtensions
{
    /// <summary>
    /// Applies pending EF Core migrations for every registered module DbContext.
    /// Call this in Program.cs before app.Run().
    /// </summary>
    public static async Task MigrateAllModulesAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var logger = app.Logger;

        // Resolve each module's DbContext and migrate it.
        // Each module registers its DbContext via its Module.cs extension method.

        // Migrate all DbContexts that are registered in DI
        var allDbContexts = services
            .GetServices<DbContext>()
            .ToList();

        if (allDbContexts.Count == 0)
        {
            logger.LogWarning("[Migration] No DbContext instances found in DI. " +
                              "Ensure each module registers its DbContext as DbContext (base type) " +
                              "or call MigrateAsync<TContext>() for each module explicitly.");
        }

        foreach (var context in allDbContexts)
        {
            var contextName = context.GetType().Name;
            try
            {
                // Some modules ship EF Core migrations; others (still early in the
                // graduation build) do not. Use migrations when they exist, and fall
                // back to EnsureCreated so every module's schema is provisioned and
                // the app can start + seed without a missing-table crash.
                var hasMigrations = context.Database.GetMigrations().Any();

                if (hasMigrations)
                {
                    logger.LogInformation("[Migration] Migrating {DbContext}...", contextName);
                    await context.Database.MigrateAsync();
                    logger.LogInformation("[Migration] {DbContext} migrated successfully.", contextName);
                }
                else
                {
                    logger.LogInformation(
                        "[Migration] {DbContext} has no migrations — ensuring schema via EnsureCreated.",
                        contextName);
                    // EnsureCreatedAsync creates the schema/tables for THIS context's model only.
                    // It is a no-op if the tables already exist. Safe because every module uses a
                    // distinct PostgreSQL schema (identity / ministry / service_req / payment / ...).
                    await context.Database.EnsureCreatedAsync();
                    logger.LogInformation("[Migration] {DbContext} schema ensured.", contextName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Migration] Failed to provision {DbContext}.", contextName);
                throw;
            }
        }
    }

    /// <summary>
    /// Migrates a specific DbContext type. Use this per-module when automatic discovery is not wired.
    /// </summary>
    public static async Task MigrateAsync<TContext>(this WebApplication app)
        where TContext : DbContext
    {
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = app.Logger;
        var contextName = typeof(TContext).Name;

        try
        {
            logger.LogInformation("[Migration] Migrating {DbContext}...", contextName);
            await context.Database.MigrateAsync();
            logger.LogInformation("[Migration] {DbContext} migrated successfully.", contextName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Migration] Failed to migrate {DbContext}.", contextName);
            throw;
        }
    }
}
