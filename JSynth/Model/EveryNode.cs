using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Globalization;

namespace JSynth.Model
{
    /// <summary>
    /// A clock: a design with one runs as a stream (see <see cref="TickNode"/>), the nodes after it again every so many
    /// seconds, until stopped or for so many ticks.
    /// </summary>
    public class EveryNode : TickNode
    {
        /// <summary>
        /// Julia code ticking, for runs and exported scripts alike.
        /// </summary>
        public static string Prelude { get; } = """
            # A clock, made by a JSynth Every node: body(t, n, show) every `seconds`, t the seconds since the start and
            # n the tick's number, until `limit` ticks (0: no end) or the stop file appears. A tick taking longer than the
            # time between ticks has the next come at once: ticks missed are not made up. show: whether to show values for
            # this one, at most four times a second, and for the last. ready() once it starts.
            function _jsynth_every(body, seconds, limit; stop = nothing, ready = () -> nothing)
                stopped() = stop !== nothing && isfile(stop)
                ready()
                start = time()
                due, n, shown = start, 0, 0.0
                while !stopped() && (limit == 0 || n < limit)
                    # Waiting in short sleeps, to see a stop soon.
                    while (wait = due - time()) > 0 && !stopped()
                        sleep(min(wait, 0.1))
                    end
                    stopped() && break
                    n += 1
                    show = n == limit || time() - shown >= 0.25
                    show && (shown = time())
                    body(time() - start, n, show)
                    due = max(due + seconds, time())
                end
                n
            end
            """.ReplaceLineEndings("\n");

        private double _seconds = 1;
        private long _limit;

        /// <summary>
        /// The time between ticks, from a hundredth of a second to a day.
        /// </summary>
        [JsonProperty]
        public double Seconds
        {
            get => _seconds;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _seconds, Math.Clamp(double.IsFinite(value) ? value : 1, 0.01, 86_400));
                this.RaisePropertyChanged(nameof(Label));
            }
        }

        /// <summary>
        /// How many ticks before the stream ends by itself; 0 for no end.
        /// </summary>
        [JsonProperty]
        public long Limit
        {
            get => _limit;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _limit, Math.Max(0, value));
                this.RaisePropertyChanged(nameof(LimitText));
            }
        }

        /// <summary>
        /// On the node: "every 2 s".
        /// </summary>
        public override string Label => $"every {Interval(Seconds)}";

        /// <summary>
        /// On the node, under it: "100 ticks", "until stopped".
        /// </summary>
        public string LimitText => Limit > 0 ? $"{Limit.ToString(CultureInfo.InvariantCulture)} {(Limit == 1 ? "tick" : "ticks")}" : "until stopped";

        private static string Interval(double seconds) => seconds switch
        {
            >= 3600 when seconds % 3600 == 0 => FormattableString.Invariant($"{seconds / 3600:0} h"),
            >= 60 when seconds % 60 == 0 => FormattableString.Invariant($"{seconds / 60:0} min"),
            _ => FormattableString.Invariant($"{seconds:0.##} s"),
        };

        public EveryNode()
        {
            Color = NodePalette.Every;
        }

        public override string TickName => "tick";

        public override bool Ends => Limit > 0;

        public override string PreludeCode => Prelude;

        public override string Start(string? stopFile)
        {
            string seconds = Seconds.ToString("R", CultureInfo.InvariantCulture);
            string limit = Limit.ToString(CultureInfo.InvariantCulture);
            return stopFile is null
                ? $"_jsynth_every({seconds}, {limit}) do _path, _n, _show"
                : $"_jsynth_every({seconds}, {limit}; stop = {ArrayFile.PathLiteral(stopFile)}, ready = {Ready}) do _path, _n, _show";
        }

        public override bool IsViewState(string property)
        {
            return base.IsViewState(property) || property == nameof(LimitText);
        }
    }
}
