namespace Fourthwall.Infrastructure.IntegrationTests;

public sealed class SqliteConnectionFactoryTests : IDisposable
{
    private readonly string _databaseDirectory;
    private readonly string _databasePath;
    private readonly SqliteConnectionFactory _factory = new();

    public SqliteConnectionFactoryTests()
    {
        _databaseDirectory = Path.Combine(Path.GetTempPath(), $"fourthwall-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_databaseDirectory);
        _databasePath = Path.Combine(_databaseDirectory, "story.db");
    }

    [Fact]
    public async Task Should_ReturnAnOpenConnection_When_PathIsValid()
    {
        await using var connection = await _factory.OpenAsync(_databasePath, TestContext.Current.CancellationToken);

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task Should_Throw_When_PathIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _factory.OpenAsync(null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_PathIsBlank(string databasePath)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _factory.OpenAsync(databasePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_Throw_When_OpenIsAlreadyCancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // The async ADO.NET path cancels via Task.FromCanceled, which surfaces the
        // OperationCanceledException-derived TaskCanceledException.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _factory.OpenAsync(_databasePath, cancelled.Token));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_databaseDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A lingering handle during teardown must not fail the test; the temp folder is disposable.
        }
    }
}
