using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using System;
using System.Threading;
using Xunit;

// The design (MainViewModel.Nodes and friends) is shared, static state: tests must not overlap.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace JuliaSynth.Tests
{
    /// <summary>
    /// Runs test code on the UI thread of a headless Avalonia app, where the undo history's timer and
    /// the view model's commands expect to live.
    /// </summary>
    public static class Ui
    {
        private static readonly Lazy<HeadlessUnitTestSession> Session = new(
            () => HeadlessUnitTestSession.StartNew(typeof(Ui)));

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .UseReactiveUI();
        }

        public static void Run(Action action)
        {
            Session.Value.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
