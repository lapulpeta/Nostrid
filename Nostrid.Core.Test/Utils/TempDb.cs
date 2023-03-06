using Microsoft.Data.Sqlite;

internal class TempDb : IDisposable
{
	private readonly SqliteConnection keepAliveConnection;
	private readonly string dbname;

	private bool disposedValue;

	public string DbName => dbname;

	public TempDb()
	{
		dbname = $"nostr.{Random.Shared.Next()}.test;mode=memory;cache=shared";
		keepAliveConnection = new SqliteConnection($"DataSource={dbname}");
		keepAliveConnection.Open();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				keepAliveConnection.Dispose();
			}

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}