using System;
using System.Globalization;
using System.Text;

namespace JuliaSynth.Runtime
{
    public enum ReportStatus
    {
        /// <summary>
        /// The value was computed.
        /// </summary>
        Ok,

        /// <summary>
        /// Computing it threw an error.
        /// </summary>
        Error,

        /// <summary>
        /// It wasn't computed, e.g. because an input failed.
        /// </summary>
        Skipped,

        /// <summary>
        /// It is being worked out: a report of its own comes when it is done.
        /// </summary>
        Running,

        /// <summary>
        /// The run paused at a breakpoint on a wire (its id the wire's), when the value came on it: until told to go on.
        /// </summary>
        Paused,
    }

    /// <summary>
    /// What a script said about one value: its Julia type and how it shows, or why it failed; and how long working it
    /// out took, in all (going through files, for every file so far), when it was.
    /// </summary>
    public sealed record Report(string Id, ReportStatus Status, string Type, string Text, double? Seconds = null);

    /// <summary>
    /// How a running script reports values back: a line starting with an unprintable marker, then tab
    /// separated id, status, type and text, with tabs, newlines and backslashes escaped, and the seconds the value
    /// took when it was timed. Scripts get the reporting functions from <see cref="Prelude"/>.
    /// </summary>
    public static class Reports
    {
        public const string Marker = "\u001eJSYNTH\t";

        /// <summary>
        /// Julia code defining the functions a script reports with:
        /// <c>_jsynth_ok(id, value)</c>, <c>_jsynth_error(id, exception)</c> and <c>_jsynth_skip(id, why)</c>.
        /// Failed and skipped ids are collected in <c>_jsynth_failed</c>, so later values can depend on them; those
        /// held back by a false condition in <c>_jsynth_held</c> as well. <c>_jsynth_start(id)</c> says a value is
        /// being worked out, and times it until it is reported.
        /// </summary>
        public static string Prelude { get; } = """
            const _JSYNTH = "\x1eJSYNTH\t"
            const _jsynth_failed = Set{String}()
            _jsynth_escape(s) = replace(String(s), "\\" => "\\\\", "\n" => "\\n", "\t" => "\\t", "\r" => "")
            # Names as Julia shows them in Main, also when the script runs in a module of its own:
            # Foo rather than Main.JuliaSynthRun.Foo.
            function _jsynth_unqualified(s)
                m = string(@__MODULE__)
                m == "Main" ? String(s) : replace(String(s), "$m." => "", "`$m`" => "`Main`", "@ $m " => "@ Main ")
            end
            # Set between the files of a stream whose values aren't shown (see the Watch folder node).
            const _jsynth_quiet = Ref(false)
            # When each value being worked out started, and the seconds each took in all (for every file so far).
            const _jsynth_started = Dict{String,UInt64}()
            const _jsynth_seconds = Dict{String,Float64}()
            function _jsynth_start(id)
                _jsynth_quiet[] || (println(stdout, _JSYNTH, id, "\trunning\t\t"); flush(stdout))
                _jsynth_started[id] = time_ns()
            end
            function _jsynth_stop(id)
                t = pop!(_jsynth_started, id, nothing)
                t === nothing || (_jsynth_seconds[id] = get(_jsynth_seconds, id, 0.0) + (time_ns() - t) / 1e9)
            end
            function _jsynth_report(id, status, type, text)
                _jsynth_stop(id)
                _jsynth_quiet[] && return
                seconds = get(_jsynth_seconds, id, nothing)
                println(stdout, _JSYNTH, id, "\t", status, "\t", _jsynth_escape(_jsynth_unqualified(type)), "\t", _jsynth_escape(_jsynth_unqualified(text)),
                    seconds === nothing ? "" : string("\t", seconds))
                flush(stdout)
            end
            function _jsynth_ok(id, value)
                # The time to show the value isn't the time to work it out.
                _jsynth_stop(id)
                # Text as it is (a saved file's path), rather than as a quoted and escaped Julia string.
                text = value isa AbstractString ? String(value) :
                    sprint(show, MIME"text/plain"(), value; context = (:limit => true, :displaysize => (12, 60)))
                _jsynth_report(id, "ok", string(typeof(value)), text)
            end
            function _jsynth_error(id, e)
                push!(_jsynth_failed, id)
                _jsynth_report(id, "error", "", sprint(showerror, e))
            end
            function _jsynth_skip(id, why)
                push!(_jsynth_failed, id)
                _jsynth_report(id, "skipped", "", why)
            end
            _jsynth_blocked(ids...) = any(in(_jsynth_failed), ids)
            # Held back by an Only when node whose condition is false: not worked out, keeping the value it had.
            const _jsynth_held = Set{String}()
            _jsynth_held_by(ids...) = any(in(_jsynth_held), ids)
            _jsynth_hold(id) = (push!(_jsynth_held, id); push!(_jsynth_failed, id); nothing)
            """.ReplaceLineEndings("\n");

        /// <summary>
        /// The report on <paramref name="line"/>, or null for ordinary output.
        /// </summary>
        public static Report? Parse(string line)
        {
            if (!line.StartsWith(Marker, StringComparison.Ordinal))
            {
                return null;
            }

            string[] fields = line[Marker.Length..].Split('\t');
            if (fields.Length is not (4 or 5))
            {
                return null;
            }

            ReportStatus? status = fields[1] switch
            {
                "ok" => ReportStatus.Ok,
                "error" => ReportStatus.Error,
                "skipped" => ReportStatus.Skipped,
                "running" => ReportStatus.Running,
                "paused" => ReportStatus.Paused,
                _ => null,
            };

            double? seconds = fields.Length == 5 && double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double s) ? s : null;
            return status is null ? null : new Report(fields[0], status.Value, Unescape(fields[2]), Unescape(fields[3]), seconds);
        }

        private static string Unescape(string text)
        {
            StringBuilder result = new(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\' && i + 1 < text.Length)
                {
                    i++;
                    _ = result.Append(text[i] switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        _ => text[i],
                    });
                }
                else
                {
                    _ = result.Append(text[i]);
                }
            }

            return result.ToString();
        }
    }
}
