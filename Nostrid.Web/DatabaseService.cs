using Microsoft.JSInterop;

namespace Nostrid;

public class DatabaseService
{
    public const string FileName = "/database/nostrid.sqlite.db";
#if RELEASE
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;
#endif



#if DEBUG
    public DatabaseService()
    {
    }
#else
    public DatabaseService(IJSRuntime jsRuntime)
    {
        if (jsRuntime == null) throw new ArgumentNullException(nameof(jsRuntime));

        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./file.js").AsTask());
    }
#endif

    public async Task InitDatabaseAsync()
    {
#if RELEASE
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("mountAndInitializeDb");
        if (!File.Exists(FileName))
        {
            File.Create(FileName).Close();
        }
#endif
    }

    private static bool saving, mustResave;
    private static object lockObj = new();

    public void StartSyncDatabase()
    {
#if RELEASE
        Task.Run(async () =>
        {
            bool oldSaving;
            lock (lockObj)
            {
                oldSaving = saving;
                if (saving)
                    mustResave = true;
                else
                    saving = true;
            }

            if (!oldSaving)
            {
                while (true)
                {
                    var module = await _moduleTask.Value;
                    await module.InvokeVoidAsync("syncDatabase");

                    lock (lockObj)
                    {
                        if (!mustResave)
                        {
                            saving = false;
                            return;
                        }
                        mustResave = false;
                    }
                }
            }
        });
#endif
    }
}
