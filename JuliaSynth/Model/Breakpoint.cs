using Newtonsoft.Json;

namespace JuliaSynth.Model
{
    /// <summary>
    /// A breakpoint on a wire: a run pauses when a value comes on it, every time or when <see cref="Condition"/> holds,
    /// until told to go on. Runs only: an exported script has none.
    /// </summary>
    /// <param name="On">Whether it pauses; off, it keeps its condition for later.</param>
    /// <param name="Condition">Julia, true or false, of <c>x</c>, the value on the wire; empty to pause every time.</param>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed record Breakpoint([property: JsonProperty] bool On, [property: JsonProperty] string Condition)
    {
        /// <summary>
        /// A new breakpoint: on, pausing every time.
        /// </summary>
        public static Breakpoint Every { get; } = new(true, string.Empty);

        /// <summary>
        /// The condition as a Julia function of the value, or <c>nothing</c> to pause every time.
        /// </summary>
        public string Julia => Condition.Trim().Length == 0 ? "nothing" : $"x -> ({Condition.Trim()})";

        /// <summary>
        /// Julia code pausing at breakpoints, for runs.
        /// </summary>
        public static string Prelude { get; } = """
            # Breakpoints on wires: pause when the value x comes on one and cond(x) holds (cond nothing: every time), showing
            # it, until JuliaSynth writes to `resume`: "continue", or "next" to pause at this wire's next value whatever its
            # condition. Ends when the run is stopped. A condition that fails doesn't pause, and is said once. source: the
            # node whose value it is, to show it on.
            const _jsynth_next_break = Ref("")
            const _jsynth_break_failed = Set{String}()
            function _jsynth_break(id, x, cond, resume, stop, source = nothing)
                if _jsynth_next_break[] != id && cond !== nothing
                    holds = try
                        cond(x) === true
                    catch e
                        id in _jsynth_break_failed || (push!(_jsynth_break_failed, id);
                            println(stderr, "A breakpoint's condition failed, so it doesn't pause: ", _jsynth_unqualified(sprint(showerror, e))))
                        false
                    end
                    holds || return
                end
                _jsynth_next_break[] = ""
                _jsynth_quiet[] = false
                # Going through files or ticks: which one, though it may not have been shown.
                isdefined(@__MODULE__, :_jsynth_progress) && _jsynth_progress[] !== nothing && _jsynth_report(_jsynth_progress[]...)
                # The value paused at, on the node it comes from, though it may not have been shown.
                source === nothing || _jsynth_ok(source, x)
                text = x isa AbstractString ? String(x) : sprint(show, MIME"text/plain"(), x; context = (:limit => true, :displaysize => (12, 60)))
                _jsynth_report(id, "paused", string(typeof(x)), text)
                while !isfile(stop)
                    if isfile(resume)
                        how = strip(read(resume, String))
                        rm(resume; force = true)
                        how == "next" && (_jsynth_next_break[] = id)
                        return
                    end
                    sleep(0.05)
                end
            end
            """.ReplaceLineEndings("\n");

        /// <summary>
        /// The file a paused run waits for, next to its stop file.
        /// </summary>
        public static string ResumeFile(string stopFile) => stopFile + ".resume";
    }
}
