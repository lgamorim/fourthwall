using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Fourthwall.Infrastructure;

/// <summary>
/// Opens connections to a story's SQLite database.
/// </summary>
/// <remarks>
/// This is the one place a connection is created, so the connection string is configured
/// consistently: <c>Foreign Keys=True</c> makes the provider enforce declared foreign keys on
/// every open — including pooled reopens — which SQLite otherwise leaves off per connection. The
/// returned connection is open and owned by the caller, who disposes it.
/// </remarks>
public sealed class SqliteConnectionFactory
{
    /// <summary>
    /// Opens a connection to the SQLite database at the given path, creating the file if it does
    /// not exist.
    /// </summary>
    /// <param name="databasePath">The path to the <c>story.db</c> file.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>An open connection with foreign-key enforcement enabled.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="databasePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="databasePath"/> is blank.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public async Task<DbConnection> OpenAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
