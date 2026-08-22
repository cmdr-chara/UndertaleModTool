using System.Collections.Concurrent;

namespace UndertaleModTool.Windows
{
    // The upstream reference-search engine also serves the WPF frontend and
    // reports progress through its MainWindow. WinUI links the same source
    // files, so provide a no-op host rather than taking a WPF dependency.
    internal sealed class MainWindow
    {
        public bool IsEnabled { get; set; } = true;

        public void InitializeProgressDialog(string title, string message)
        {
        }

        public void SetProgressBar(object? value, string label, int minimum, int maximum)
        {
        }

        public void StartProgressBarUpdater()
        {
        }

        public void IncrementProgressParallel()
        {
        }

        public Task StopProgressBarUpdater() => Task.CompletedTask;

        public void HideProgressBar()
        {
        }
    }

    // The fork previously made the unreferenced-assets accumulator thread-safe.
    // Upstream still mutates a List from Parallel.ForEach. For the WinUI-linked
    // copy, run those partitions serially so the latest reference predicates
    // can be used without reintroducing that data race.
    internal static class Parallel
    {
        public static void ForEach<TSource>(IEnumerable<TSource> source, Action<TSource> body)
        {
            foreach (TSource item in source)
                body(item);
        }

        public static void ForEach<TSource>(Partitioner<TSource> source, Action<TSource> body)
        {
            foreach (TSource item in source.GetDynamicPartitions())
                body(item);
        }
    }
}

namespace System.Windows
{
    internal sealed class Application
    {
        public static Application Current { get; } = new();

        public object MainWindow { get; } = new UndertaleModTool.Windows.MainWindow();
    }
}
