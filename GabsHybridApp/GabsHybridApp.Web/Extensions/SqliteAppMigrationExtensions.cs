using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GabsHybridApp.Web.Extensions;

public static class SqliteAppMigrationExtensions
{
    public static WebApplication MigrateDb<TContext>(
        this WebApplication app,
        bool enableWal = false,
        Action<TContext>? seed = null)
        where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();
        var factory = scope.ServiceProvider.GetService<IDbContextFactory<TContext>>();
        if (factory is not null)
        {
            using var db = factory.CreateDbContext();
            Apply(db, enableWal, seed);
        }
        else
        {
            using var db = scope.ServiceProvider.GetRequiredService<TContext>();
            Apply(db, enableWal, seed);
        }
        return app;
    }

    private static void Apply<TContext>(TContext db, bool enableWal, Action<TContext>? seed)
        where TContext : DbContext
    {
        try
        {
            if (db.Database.IsNpgsql())
            {
                try
                {
                    db.Database.ExecuteSqlRaw(@"
                        DO $$
                        BEGIN
                            CREATE SCHEMA IF NOT EXISTS ""GabsHybridApp"";
                            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE (table_schema = 'GabsHybridApp' OR table_schema = 'public') AND LOWER(table_name) IN ('products', 'useraccounts', 'backuprecords')) THEN
                                CREATE TABLE IF NOT EXISTS ""GabsHybridApp"".""__EFMigrationsHistory"" (
                                    ""MigrationId"" character varying(150) NOT NULL,
                                    ""ProductVersion"" character varying(32) NOT NULL,
                                    CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
                                );
                                INSERT INTO ""GabsHybridApp"".""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                                VALUES ('20260917195851_InitialCreate', '10.0.9')
                                ON CONFLICT (""MigrationId"") DO NOTHING;
                            END IF;
                        END $$;
                    ");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NOTICE] PostgreSQL pre-migration check notice: {ex.Message}");
                }
            }

            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            if (db.Database.IsSqlite())
            {
                Console.WriteLine($"[NOTICE] SQLite database migration notice: {ex.Message}");
            }
            else
            {
                Console.WriteLine($"[ERROR] Database migration failed: {ex.Message}");
                throw;
            }
        }

        // Optional: enable WAL only for SQLite
        if (enableWal && db.Database.IsSqlite())
        {
            try
            {
                db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            }
            catch { }
        }

        // Run seed
        try
        {
            seed?.Invoke(db);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Database seed notice: {ex.Message}");
        }
    }
}
