using GabsHybridApp.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GabsHybridApp.Web.Extensions;

public sealed class WebDesignTimeFactory : IDesignTimeDbContextFactory<HybridAppDbContext>
{
    public HybridAppDbContext CreateDbContext(string[] args)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var basePath = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
        {
            var webProjDir = Path.Combine(basePath, "GabsHybridApp", "GabsHybridApp.Web");
            if (Directory.Exists(webProjDir) && File.Exists(Path.Combine(webProjDir, "appsettings.json")))
            {
                basePath = webProjDir;
            }
        }

        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false);

        if (!string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
        }

        var configuration = builder
            .AddUserSecrets(typeof(WebDesignTimeFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .BuildServiceProvider();

        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var connString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection in appsettings.");

        var schema = configuration.GetConnectionString("Schema");

        var cmdLine = Environment.CommandLine;
        var isSharedProject = cmdLine.Contains("GabsHybridApp.Shared", StringComparison.OrdinalIgnoreCase);
        if (isSharedProject)
        {
            var dbPath = Path.Combine(basePath, "Data", "design.db");
            var sqliteOptions = new DbContextOptionsBuilder<HybridAppDbContext>()
                .UseSqlite($"Data Source={dbPath}", sqlite => sqlite.MigrationsAssembly("GabsHybridApp.Shared"))
                .UseApplicationServiceProvider(services)
                .Options;
            return new HybridAppDbContext(sqliteOptions);
        }

        var connMatch = System.Text.RegularExpressions.Regex.Match(cmdLine, @"--connection\s+""?([^""\r\n]+?)""?(?=\s+--|\s*$)");
        if (connMatch.Success && !string.IsNullOrWhiteSpace(connMatch.Groups[1].Value))
        {
            connString = connMatch.Groups[1].Value.Trim().Trim('"');
        }

        var isPostgres = connString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                         connString.Contains("Port=", StringComparison.OrdinalIgnoreCase);

        var optionsBuilder = new DbContextOptionsBuilder<HybridAppDbContext>();

        if (isPostgres)
        {
            optionsBuilder.UseNpgsql(connString, sql =>
            {
                sql.MigrationsAssembly(typeof(WebDesignTimeFactory).Assembly.GetName().Name);
                if (!string.IsNullOrWhiteSpace(schema))
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            });
        }
        else
        {
            optionsBuilder.UseSqlServer(connString, sql =>
            {
                sql.MigrationsAssembly(typeof(WebDesignTimeFactory).Assembly.GetName().Name);
                if (!string.IsNullOrWhiteSpace(schema))
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            });
        }

        optionsBuilder.UseApplicationServiceProvider(services);

        var db = new HybridAppDbContext(optionsBuilder.Options);

        try
        {
            if (db.Database.IsNpgsql())
            {
                db.Database.ExecuteSqlRaw(@"
                    DO $$
                    BEGIN
                        CREATE SCHEMA IF NOT EXISTS ""GabsHybridApp"";
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE (table_schema = 'GabsHybridApp' OR table_schema = 'public') AND LOWER(table_name) IN ('backuprecords', 'products', 'useraccounts')) THEN
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      [BASELINE NOTICE] {ex.Message}");
        }

        return db;
    }
}

