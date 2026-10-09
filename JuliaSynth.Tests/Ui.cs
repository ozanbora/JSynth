using Avalonia;
using Avalonia.Headless;
using ReactiveUI.Avalonia;
using System;
using System.Threading;
using System.Threading.Tasks;
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
                .WithInterFont()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .UseReactiveUI(_ => { });
        }

        public static void Run(Action action)
        {
            Session.Value.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Runs asynchronous test code on the UI thread, until it is done.
        /// </summary>
        public static void Run(Func<Task> action)
        {
            Session.Value.Dispatch(async () =>
            {
                await action();
                return true;
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
