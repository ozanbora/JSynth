using Avalonia.Threading;
using JuliaSynth.Model;
using JuliaSynth.Runtime;
using ReactiveUI;
using ReactiveUI.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JuliaSynth.ViewModels;

/// <summary>
/// Packages a design needs: checked when it opens, so a missing one is offered for installing before the
/// first run rather than at it, and installed with the progress in sight.
/// </summary>
public partial class MainViewModel
{
    private enum InstallOutcome
    {
        Installed,
        Failed,
        Stopped,
    }

    private IReadOnlyList<string> _missingPackages = [];
    private IReadOnlyList<string> _installingPackages = [];
    private bool _installFailed;

    // Goes up with every design opened, so a check finishing after the next one opened is dropped.
    private int _openedDesigns;

    /// <summary>
    /// Packages the design needs that Julia can't load, as last checked; offered for installing.
    /// </summary>
    public IReadOnlyList<string> MissingPackages
    {
        get => _missingPackages;
        private set
        {
            _missingPackages = value;
            _installFailed = false;
            RaisePackagesChanged();
        }
    }

    /// <summary>
    /// The banner over the canvas, about missing packages or their installing.
    /// </summary>
    public bool IsPackageBannerVisible => MissingPackages.Count > 0 || IsInstallingPackages;

    public bool IsInstallingPackages => _installingPackages.Count > 0;

    public bool CanOfferInstall => !IsInstallingPackages;

    public string PackageBannerText
    {
        get
        {
            if (IsInstallingPackages)
            {
                return $"Installing {Names(_installingPackages)} with Julia's package manager. The first time, this can take a few minutes; the Output panel shows how it goes.";
            }

            bool one = MissingPackages.Count == 1;
            return _installFailed
                ? $"Installing {Names(MissingPackages)} failed: the Output panel says why. The nodes needing {(one ? "it" : "them")} are skipped when the design runs."
                : $"This design needs {Names(MissingPackages)}, which {(one ? "isn't" : "aren't")} installed. The nodes needing {(one ? "it" : "them")} are skipped until {(one ? "it is" : "they are")}.";
        }
    }

    public ReactiveCommand<RxVoid, RxVoid> InstallDesignPackages { get; private set; } = null!;

    public ReactiveCommand<RxVoid, RxVoid> DismissPackages { get; private set; } = null!;

    private void CreatePackageCommands()
    {
        InstallDesignPackages = ReactiveCommand.CreateFromTask(InstallDesignPackagesAsync, this.WhenAnyValue(x => x.IsRunning, busy => !busy));
        DismissPackages = ReactiveCommand.Create(() => { MissingPackages = []; });
    }

    private void RaisePackagesChanged()
    {
        this.RaisePropertyChanged(nameof(MissingPackages));
        this.RaisePropertyChanged(nameof(IsPackageBannerVisible));
        this.RaisePropertyChanged(nameof(IsInstallingPackages));
        this.RaisePropertyChanged(nameof(CanOfferInstall));
        this.RaisePropertyChanged(nameof(PackageBannerText));
    }

    /// <summary>
    /// A design was opened: say which packages it needs that aren't installed, without waiting for a run.
    /// </summary>
    private void DesignOpened()
    {
        _openedDesigns++;
        MissingPackages = [];
        _ = CheckDesignPackagesAsync();
    }

    /// <summary>
    /// Checks the design's packages in the session's Julia (quietly: the banner says what is missing).
    /// </summary>
    private async Task CheckDesignPackagesAsync()
    {
        if (Julia is not JuliaInstallation julia || _session is not JuliaSession session)
        {
            // Checked once Julia is found.
            return;
        }

        int opened = _openedDesigns;
        IReadOnlyList<string> packages = ScriptGenerator.Generate(Nodes, Connections, instrumented: false).Packages;
        List<string> unknown = packages.Where(p => !_installedPackages.Contains(p)).ToList();
        if (unknown.Count == 0)
        {
            return;
        }

        IReadOnlyList<string> missing;
        try
        {
            missing = await new JuliaPackages(julia.Path, session).FindMissingAsync(unknown, RunFolder);
        }
        catch (Exception e) when (e is InvalidOperationException or OperationCanceledException or ObjectDisposedException
            or System.IO.IOException or System.ComponentModel.Win32Exception)
        {
            // A run checks again, and says what went wrong.
            return;
        }

        // Another design (or Julia) by now.
        if (opened != _openedDesigns || julia != Julia)
        {
            return;
        }

        _installedPackages.UnionWith(unknown.Except(missing));
        if (!IsInstallingPackages)
        {
            MissingPackages = missing;
        }
    }

    /// <summary>
    /// Installs the packages the banner offers.
    /// </summary>
    public async Task InstallDesignPackagesAsync()
    {
        if (MissingPackages.Count == 0 || await StartAsync() is not (JuliaInstallation julia, CancellationTokenSource cancellation))
        {
            return;
        }

        try
        {
            _ = await InstallPackagesAsync(julia, MissingPackages, RunFolder, cancellation.Token);
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Log($"Julia couldn't be started: {e.Message}", OutputKind.Error);
        }
        finally
        {
            Finish(cancellation);
        }
    }

    /// <summary>
    /// Installs <paramref name="packages"/> with Julia's package manager, in a Julia of its own, showing
    /// how it goes on the banner and in the output.
    /// </summary>
    private async Task<InstallOutcome> InstallPackagesAsync(JuliaInstallation julia, IReadOnlyList<string> packages, string folder, CancellationToken cancellationToken)
    {
        string names = string.Join(", ", packages);
        string them = packages.Count == 1 ? "it" : "them";
        _installingPackages = packages;
        RaisePackagesChanged();
        RunResult result;
        try
        {
            Log($"Installing {names} with Julia's package manager; the first time, this can take a few minutes…", OutputKind.Info);
            result = await new JuliaPackages(julia.Path).InstallAsync(packages, folder, line => Dispatcher.UIThread.Post(() => Log(line.Text, OutputKind.Output)), cancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        finally
        {
            _installingPackages = [];
            RaisePackagesChanged();
        }

        if (result.Cancelled)
        {
            Log("Installing stopped.", OutputKind.Info);
            return InstallOutcome.Stopped;
        }

        if (!result.Succeeded)
        {
            Log($"Installing {names} failed (see above); the nodes needing {them} are skipped.", OutputKind.Error);
            _installFailed = true;
            RaisePackagesChanged();
            return InstallOutcome.Failed;
        }

        _installedPackages.UnionWith(packages);
        MissingPackages = MissingPackages.Except(packages).ToList();

        // Loaded afresh by a new Julia, rather than by one that looked for them before they were there.
        _session?.Restart();
        ForgetWarmUp();
        _ = WarmUpAsync();
        Log($"Installed {names} in {result.Elapsed.TotalSeconds:0} s.", OutputKind.Info);
        return InstallOutcome.Installed;
    }

    private static string Names(IReadOnlyList<string> packages)
    {
        return packages.Count switch
        {
            0 => string.Empty,
            1 => packages[0],
            _ => $"{string.Join(", ", packages.SkipLast(1))} and {packages[^1]}",
        };
    }
}
