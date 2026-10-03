using System;
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
    }

    /// <summary>
    /// What a script said about one value: its Julia type and how it shows, or why it failed.
    /// </summary>
    public sealed record Report(string Id, ReportStatus Status, string Type, string Text);

    /// <summary>
    /// How a running script reports values back: a line starting with an unprintable marker, then tab
    /// separated id, status, type and text, with tabs, newlines and backslashes escaped. Scripts get the
    /// reporting functions from <see cref="Prelude"/>.
    /// </summary>
    public static class Reports
    {
        public const string Marker = "\u001eJSYNTH\t";

        /// <summary>
        /// Julia code defining the functions a script reports with:
        /// <c>_jsynth_ok(id, value)</c>, <c>_jsynth_error(id, exception)</c> and <c>_jsynth_skip(id, why)</c>.
        /// Failed and skipped ids are collected in <c>_jsynth_failed</c>, so later values can depend on them.
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
            function _jsynth_report(id, status, type, text)
                _jsynth_quiet[] && return
                println(stdout, _JSYNTH, id, "\t", status, "\t", _jsynth_escape(_jsynth_unqualified(type)), "\t", _jsynth_escape(_jsynth_unqualified(text)))
                flush(stdout)
            end
            function _jsynth_ok(id, value)
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
            if (fields.Length != 4)
            {
                return null;
            }

            ReportStatus? status = fields[1] switch
            {
                "ok" => ReportStatus.Ok,
                "error" => ReportStatus.Error,
                "skipped" => ReportStatus.Skipped,
                _ => null,
            };

            return status is null ? null : new Report(fields[0], status.Value, Unescape(fields[2]), Unescape(fields[3]));
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
