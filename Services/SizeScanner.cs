using DevClean.Models;

namespace DevClean.Services;

public static class SizeScanner
{
    public static async Task ScanAsync(
        IReadOnlyList<CleanItem> items,
        IProgress<(int Done, int Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var throttler = new SemaphoreSlim(4);
        int completed = 0;
        int total = items.Count;

        var tasks = items.Select(async item =>
        {
            await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() => ScanItem(item), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                throttler.Release();
                int done = Interlocked.Increment(ref completed);
                progress?.Report((done, total));
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        throttler.Dispose();
    }

    public static void ScanItem(CleanItem item)
    {
        item.ResetState();

        if (item.IsCommand)
        {
            item.Available = item.RequiresCommand is null || PathResolver.CommandExists(item.RequiresCommand);
            item.SizeBytes = 0;
            return;
        }

        IReadOnlyList<string> paths = PathResolver.ExpandAll(item.Paths);
        long size = 0;
        foreach (string path in paths)
        {
            size += PathResolver.GetSize(path);
        }

        item.SizeBytes = size;
        item.Available = paths.Count > 0;
    }
}
