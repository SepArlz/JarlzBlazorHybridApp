using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GabsHybridApp.Shared.Data;

public static class SqliteOptionsExtensions
{
    // 1) With the optional builder
    public static DbContextOptionsBuilder UseSqlite(
        this DbContextOptionsBuilder options,
        string connectionStringOrPath,
        string basePath,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
    {
        var normalized = Normalize(connectionStringOrPath, basePath);
        return Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions
               .UseSqlite(options, normalized, sqliteOptionsAction);
    }

    // 2) Generic overload
    public static DbContextOptionsBuilder<TContext> UseSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> options,
        string connectionStringOrPath,
        string basePath,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
        where TContext : DbContext
    {
        var normalized = Normalize(connectionStringOrPath, basePath);
        return Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions
               .UseSqlite(options, normalized, sqliteOptionsAction);
    }

    // 3) Overload without the builder (still requires basePath to avoid ambiguity)
    public static DbContextOptionsBuilder UseSqlite(
        this DbContextOptionsBuilder options,
        string? connectionStringOrPath,
        string? basePath)
        => options.UseSqlite(connectionStringOrPath ?? string.Empty, basePath ?? string.Empty, sqliteOptionsAction: null);

    public static DbContextOptionsBuilder<TContext> UseSqlite<TContext>(
        this DbContextOptionsBuilder<TContext> options,
        string? connectionStringOrPath,
        string? basePath)
        where TContext : DbContext
        => options.UseSqlite(connectionStringOrPath ?? string.Empty, basePath ?? string.Empty, sqliteOptionsAction: null);

    private static string Normalize(string connectionStringOrPath, string basePath)
    {
        // Accept either "Data Source=..." or bare relative path like "App_Data/web.db"
        var csb = connectionStringOrPath.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? new SqliteConnectionStringBuilder(connectionStringOrPath)
            : new SqliteConnectionStringBuilder { DataSource = connectionStringOrPath };

        var ds = csb.DataSource;
        var fullPath = Path.IsPathRooted(ds) ? ds : Path.Combine(basePath, ds);

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        csb.DataSource = fullPath;
        return csb.ToString();
    }
}