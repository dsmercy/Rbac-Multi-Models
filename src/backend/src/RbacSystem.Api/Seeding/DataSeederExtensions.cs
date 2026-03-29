namespace RbacSystem.Api.Infrastructure;

public static class DataSeederExtensions
{
    /// <summary>
    /// Runs the idempotent DataSeeder in Development only.
    /// Call after app.MapControllers() and before app.Run().
    ///
    /// Requires DataSeeder to be registered in DI first:
    ///   builder.Services.AddTransient&lt;DataSeeder&gt;();
    /// </summary>
    public static async Task SeedDevelopmentDataAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DataSeeder>>();

        try
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
            await seeder.SeedAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[Seeder] Seed failed — app will continue but test data may be incomplete.");
            throw; // rethrow to fail fast in development — prevents confusion from partial seeding
        }
    }
}


// =============================================================================
// Program.cs addition — add these two lines just before app.Run():
//
//   await app.SeedDevelopmentDataAsync();
//   app.Run();
//
// Full context (replace existing app.Run() at the bottom of Program.cs):
// =============================================================================

/*

// ---------- paste this block at the bottom of Program.cs ----------

app.MapControllers();

// Seed test data in Development only (idempotent — safe to restart)
await app.SeedDevelopmentDataAsync();

app.Run();

// ------------------------------------------------------------------
*/