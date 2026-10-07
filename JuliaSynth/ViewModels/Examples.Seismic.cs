using JuliaSynth.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// The seismology examples: the 2019 Ridgecrest earthquake, at one station and a network of four.
    /// </summary>
    public static partial class Examples
    {
        /// <summary>
        /// One-minute files of station PFO's vertical seismometer on 4 July 2019: 3 hours from 15:40 UTC, the M6.4
        /// Ridgecrest earthquake 200 km away at 17:33:49, its M4.0 foreshock at 17:02:55.
        /// </summary>
        private static readonly string[] SeismogramMinutes = [.. Enumerable.Range(0, 180)
            .Select(i => $"PFO.BHZ.{new DateTime(2019, 7, 4, 15, 40, 0).AddMinutes(i).ToString("yyyy-MM-dd'T'HH-mm", System.Globalization.CultureInfo.InvariantCulture)}.csv")];

        /// <summary>
        /// The time a seismogram minute's file starts at: PFO.BHZ.2019-07-04T17-03.csv, 2019-07-04T17:03:00.
        /// </summary>
        private static DateTime MinuteOf(string name) =>
            DateTime.ParseExact(name["PFO.BHZ.".Length..^".csv".Length], "yyyy-MM-dd'T'HH-mm", System.Globalization.CultureInfo.InvariantCulture);

        private static string Stamp(DateTime time) => time.ToString("yyyy-MM-dd'T'HH':'mm':'ss", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Station PFO around the Ridgecrest earthquakes, from EarthScope's FDSN web service, a file a minute: the hour
        /// with the foreshock and the M6.4, or 3 hours.
        /// </summary>
        internal static ExampleData RidgecrestSeismograms { get; } = new(
            "Ridgecrest seismograms",
            "The vertical seismometer of station PFO at Piñon Flat, California, on 4 July 2019, 20 readings a second: the M6.4 "
                + "Ridgecrest earthquake, 200 km away, struck at 17:33:49 UTC, 31 minutes after an M4.0 foreshock. A file a "
                + "minute, as a station's data logger writes them, each a GeoCSV file: lines starting with # describe the station, "
                + "then a line \"Sample\" and the 1,200 readings, in counts.",
            "Station PFO of the Global Seismograph Network (IRIS/IDA, network II), operated by the Scripps Institution of "
                + "Oceanography: Scripps Institution of Oceanography (1986), Global Seismograph Network - IRIS/IDA, International "
                + "Federation of Digital Seismograph Networks, https://doi.org/10.7914/SN/II. Openly available from the EarthScope "
                + "Consortium's FDSN web services (https://service.earthscope.org/fdsnws/dataselect/1/), saved unchanged.",
            "https://service.earthscope.org/fdsnws/dataselect/1/query",
            [
                new("Short",
                    "The hour from 16:40 UTC: the foreshock, then the M6.4. 60 files.",
                    SeismogramMinutes[60..120],
                    450_000),
                new("Full",
                    "3 hours from 15:40 UTC: a quiet hour before, and the hour of aftershocks after. 180 files.",
                    SeismogramMinutes,
                    1_350_000),
            ])
        {
            FileUrl = name => $"https://service.earthscope.org/fdsnws/dataselect/1/query?net=II&sta=PFO&loc=00&cha=BHZ"
                + $"&start={Stamp(MinuteOf(name))}&end={Stamp(MinuteOf(name).AddMinutes(1))}&format=geocsv.slist",
        };

        /// <summary>
        /// The stations of the seismic network example: code, network, location code. PFO first, as in the Earthquake
        /// detector; GSC (Goldstone), ISA (Isabella) and TIN (Tinemaha) of the Southern California network.
        /// </summary>
        private static readonly (string Code, string Network, string Location)[] NetworkStations =
            [("PFO", "II", "00"), ("GSC", "CI", "--"), ("ISA", "CI", "--"), ("TIN", "CI", "--")];

        /// <summary>
        /// The network's minute files: each station's in a folder of its own, of the same names: PFO/2019-07-04T17-03.csv.
        /// </summary>
        private static string[] NetworkMinutes(int from, int count) => [.. NetworkStations.SelectMany(s => Enumerable.Range(from, count)
            .Select(i => $"{s.Code}/{new DateTime(2019, 7, 4, 15, 40, 0).AddMinutes(i).ToString("yyyy-MM-dd'T'HH-mm", System.Globalization.CultureInfo.InvariantCulture)}.csv"))];

        /// <summary>
        /// Four stations' seismograms around the Ridgecrest earthquakes, a file a minute each, from EarthScope's FDSN web
        /// service: the hour with the foreshock and the M6.4, or 3 hours.
        /// </summary>
        internal static ExampleData RidgecrestNetwork { get; } = new(
            "Ridgecrest network",
            "The vertical seismometers of four stations in southern California on 4 July 2019, as the M6.4 Ridgecrest "
                + "earthquake struck at 17:33:49 UTC, 31 minutes after an M4.0 foreshock: PFO at Piñon Flat (200 km away, "
                + "20 readings a second), GSC at Goldstone (80 km), ISA at Isabella (90 km) and TIN at Tinemaha (190 km), 40 "
                + "readings a second. A file a minute for each, in a folder each, of the same names, GeoCSV: lines starting "
                + "with # describe the station, then a line \"Sample\" and the readings, in counts. The web service gives some "
                + "minutes in pieces; they are joined into one.",
            "Station PFO of the Global Seismograph Network (IRIS/IDA, network II), operated by the Scripps Institution of "
                + "Oceanography: https://doi.org/10.7914/SN/II. Stations GSC, ISA and TIN of the Southern California Seismic "
                + "Network (network CI), operated by Caltech and the USGS: California Institute of Technology and United States "
                + "Geological Survey Pasadena (1926), Southern California Seismic Network, https://doi.org/10.7914/SN/CI. Openly "
                + "available from the EarthScope Consortium's FDSN web services (https://service.earthscope.org/fdsnws/dataselect/1/); "
                + "the readings are unchanged, a minute's pieces joined.",
            "https://service.earthscope.org/fdsnws/dataselect/1/query",
            [
                new("Short",
                    "The hour from 16:40 UTC: the foreshock, then the M6.4. 240 files, 60 a station: about a minute to download.",
                    NetworkMinutes(60, 60),
                    2_400_000),
                new("Full",
                    "3 hours from 15:40 UTC: a quiet hour before, and the hour of aftershocks after. 720 files, 180 a station: about 4 minutes to download.",
                    NetworkMinutes(0, 180),
                    7_200_000),
            ])
        {
            FileUrl = name =>
            {
                (string code, string network, string location) = NetworkStations.Single(s => name.StartsWith(s.Code + "/", StringComparison.Ordinal));
                DateTime minute = DateTime.ParseExact(name[(code.Length + 1)..^".csv".Length], "yyyy-MM-dd'T'HH-mm", System.Globalization.CultureInfo.InvariantCulture);
                return $"https://service.earthscope.org/fdsnws/dataselect/1/query?net={network}&sta={code}&loc={location}&cha=BHZ"
                    + $"&start={Stamp(minute)}&end={Stamp(minute.AddMinutes(1))}&format=geocsv.slist";
            },
            Convert = (_, from, to) => ExampleData.JoinGeoCsv(from, to),
        };

        private static MainViewModel.DesignDocument EarthquakeDetector()
        {
            // The full set when it was downloaded, else the short one, a minute at a time.
            string folder = RidgecrestSeismograms.DownloadedFolder ?? ExampleData.SetFolder(RidgecrestSeismograms.Folder, RidgecrestSeismograms.Sets[0]);
            FilesNode files = new()
            {
                Format = FileFormats.Csv, Pattern = "PFO.BHZ.*.csv", Shape = JuliaTypes.Vector, HasHeader = true, HasComments = true,
                Folder = folder, EachFile = true, Row = 6, Column = Inputs,
            };
            PlotNode seismogram = new() { Title = "The minute's seismogram (counts)", Kind = "Line", Row = 23, Column = Stage2 };

            // The background: the typical energy of a second over the last 5 minutes.
            Function energy = Define("energy", "Vector{Float64}", 6, Stage1,
                """
                # The energy of each second: the mean square of the changes from reading to reading (20 a second),
                # which leaves out the slow swell of the ocean's microseism.
                d = diff(x)
                [sum(abs2, d[(s - 1) * 20 + 1:s * 20]) / 20 for s in 1:length(d) ÷ 20]
                """,
                ("x", "Vector{Float64}"));
            Function level = Define("level", "Float64", 6, Stage2,
                "sort(e)[cld(length(e), 2)]  # the minute's typical second: the median, which a short quake hardly moves",
                ("e", "Vector{Float64}"));
            WindowNode last = new() { Size = 5, Row = 6, Column = Stage3 };
            Function background = Define("background", "Float64", 6, Stage3 + 14, "sum(W) / length(W)  # the last 5 minutes' typical energy", ("W", "Vector"));

            // How much each second shook over the background, and whether the minute had a quake.
            Function ratio = Define("ratio", "Vector{Float64}", 17, Stage3 + 28, "sqrt.(e ./ B)  # each second's shaking over the background's, as amplitudes", ("e", "Vector{Float64}"), ("B", "Float64"));
            Function loudest = Define("loudest", "Float64", 17, Stage3 + 42, "maximum(r)", ("r", "Vector{Float64}"));
            Function quake = Define("quake", "Bool", 29, Stage3 + 42, "m > 4  # 4 times the background's shaking", ("m", "Float64"));

            // A new quake: one the minute before didn't have (a quake's shaking lasts into the next minute).
            Integer before = Whole(0, 29);
            Function shook = Define("shook", "Int64", 29, Stage3 + 56, "Int(q)  # whether this minute had a quake, for the next", ("q", "Bool"));
            Function newQuake = Define("new_quake", "Bool", 41, Stage3 + 56, "q && b == 0  # a quake the minute before didn't have", ("q", "Bool"), ("b", "Int64"));
            Integer quakes = Whole(0, 41);
            Function tally = Define("tally", "Int64", 41, Stage3 + 70, "n + q  # the quakes so far", ("n", "Int64"), ("q", "Bool"));

            // Each new quake noted: when, from the file's name and the loudest second, and how strong; saved a row each.
            (Node, string) name = (files, FilesNode.NameOutput);
            Function note = Define("note", Function.AnyType, 53, Stage3 + 42,
                """
                # When, from the file's name (PFO.BHZ.2019-07-04T17-03.csv) and the loudest second, and how strong.
                s = argmax(r) - 1
                string(name[9:18], " ", name[20:21], ":", name[23:24], ":", lpad(s, 2, '0'), " UTC, ", round(Int, maximum(r)), "× the background")
                """,
                ("name", Function.AnyType), ("r", "Vector{Float64}"));
            WhenNode noted = new() { Row = 53, Column = Stage3 + 56 };
            DisplayNode shown = Shown(53, Stage3 + 72);
            SaveFile saved = new() { Path = System.IO.Path.Combine(RidgecrestSeismograms.Folder, "quakes.csv"), PerFile = true, Row = 65, Column = Stage3 + 70 };

            // The loudest second of every minute so far, as a power of 10.
            ArrayBuilder trend = Empty(JuliaTypes.Vector, "Float64", 77);
            Function grow = Define("grow", "Vector{Float64}", 77, Stage3 + 56, "[T; log10(m)]  # the loudest second of every minute so far, as a power of 10", ("T", "Vector{Float64}"), ("m", "Float64"));
            PlotNode loudness = new() { Title = "Loudest second each minute (log10 of × the background)", Kind = "Line", Row = 79, Column = Stage3 + 74 };

            return Design(
                [files, seismogram, energy, level, last, background, ratio, loudest, quake, before, shook, newQuake, quakes, tally,
                    note, noted, shown, saved, trend, grow, loudness],
                (files, seismogram, "y"), (files, energy, 0), (energy, level, 0), (level, last, 0), (last, background, 0),
                (energy, ratio, 0), (background, ratio, 1), (ratio, loudest, 0), (loudest, quake, 0),
                (quake, shook, 0), (shook, before, "next"), (quake, newQuake, 0), (before, newQuake, 1),
                (quakes, tally, 0), (newQuake, tally, 1), (tally, quakes, "next"),
                (name, note, 0), (ratio, note, 1), (note, noted, "value"), (newQuake, noted, "condition"), (noted, shown, 0), (noted, saved, 0),
                (trend, grow, 0), (loudest, grow, 1), (grow, trend, "next"), (grow, loudness, "y"));
        }

        private static MainViewModel.DesignDocument EarthquakeNetwork()
        {
            // The full set when it was downloaded, else the short one: each station's minutes, zipped by name.
            string folder = RidgecrestNetwork.DownloadedFolder ?? ExampleData.SetFolder(RidgecrestNetwork.Folder, RidgecrestNetwork.Sets[0]);
            FilesNode[] stations = [.. NetworkStations.Select((s, i) => new FilesNode
            {
                Format = FileFormats.Csv, Pattern = "*.csv", Shape = JuliaTypes.Vector, HasHeader = true, HasComments = true, EachFile = true,
                Folder = System.IO.Path.Combine(folder, s.Code),
                Row = 6 + (12 * i), Column = Inputs,
            })];
            ZipNode minutes = new() { Row = 6, Column = Stage1 };

            // Each station's background: the typical energy of a second over the last 5 minutes, those before this one.
            Function energies = Define("energies", "Vector", 6, Stage1,
                """
                # Each station's energy of each second: the mean square of the changes from reading to reading, which
                # leaves out the slow swell of the ocean. A station's readings a second: its minute's over 60.
                map(F) do x
                    fs = length(x) ÷ 60
                    d = diff(x)
                    [sum(abs2, d[(s - 1) * fs + 1:s * fs]) / fs for s in 1:length(d) ÷ fs]
                end
                """,
                ("F", "Vector"));
            Function levels = Define("levels", "Vector{Float64}", 6, Stage2,
                "[sort(e)[cld(length(e), 2)] for e in E]  # each station's typical second: the median, which a short quake hardly moves",
                ("E", "Vector"));
            WindowNode last = new() { Size = 5, Row = 6, Column = Stage3 };
            Function background = Define("background", "Vector{Float64}", 6, Stage3 + 14, "sum(W) / length(W)  # each station's, over the last 5 minutes", ("W", "Vector"));
            ArrayBuilder before = Empty(JuliaTypes.Vector, "Float64", 18);

            // How much each second shook at each station, against the background of the minutes before.
            Function ratios = Define("ratios", "Vector", 18, Stage2,
                "isempty(B) ? [zeros(length(e)) for e in E] : [sqrt.(e ./ b) for (e, b) in zip(E, B)]  # as amplitudes; nothing the first minute",
                ("E", "Vector"), ("B", "Vector{Float64}"));
            Function loudest = Define("loudest", "Vector{Int64}", 18, Stage3, "[round(Int, maximum(r)) for r in R]  # each station's loudest second, × the background", ("R", "Vector"));
            DisplayNode loud = Shown(19, Stage3 + 16);

            // A station hears a new quake: a second 4 times the background, the minute before it having none.
            ArrayBuilder shookBefore = Zeros(4, 30);
            Function shook = Define("shook", "Vector{Float64}", 30, Stage3, "Float64[maximum(r) > 4 for r in R]  # whether each station shook this minute, for the next", ("R", "Vector"));
            (Node, string) name = (stations[0], FilesNode.NameOutput);
            Function heard = Define("heard", "Vector{Float64}", 30, Stage2,
                """
                # When each station heard a new quake, in seconds of the day; 0: none. The minute is the file's name's.
                m = parse(Int, name[12:13]) * 3600 + parse(Int, name[15:16]) * 60
                [maximum(r) > 4 && b == 0 ? m + argmax(r) - 1.0 : 0.0 for (r, b) in zip(R, B)]
                """,
                ("R", "Vector"), ("B", "Vector{Float64}"), ("name", Function.AnyType));

            // A quake the network confirms: heard by 2 stations or more within 60 s, this minute or the one before.
            ArrayBuilder heardBefore = Zeros(4, 54);
            Integer noted = new() { SelectedType = "64-bit", Value = 0, Row = 66, Column = Inputs };
            Function confirmed = Define("confirmed", "Float64", 42, Stage3,
                """
                # The time of a quake 2 stations or more heard within 60 s, this minute (D) or the one before (P), not noted
                # yet (the last at L): its earliest station's, in seconds of the day; 0: none. A wave's arrivals at stations
                # far apart can be a minute apart; one station alone may be heavy traffic or wind.
                for (i, t) in enumerate(D)
                    t > 0 && t > L + 60 || continue
                    near = [(j, u) for (j, u) in [collect(enumerate(D)); collect(enumerate(P))] if u > 0 && abs(u - t) <= 60]
                    length(unique(first.(near))) >= 2 && return minimum(last.(near))
                end
                0.0
                """,
                ("D", "Vector{Float64}"), ("P", "Vector{Float64}"), ("L", "Int64"));
            Function kept = Define("kept", "Int64", 66, Stage3 + 14, "c > 0 ? round(Int, c) : L  # the last quake noted", ("c", "Float64"), ("L", "Int64"));
            Function quake = Define("quake", "Bool", 54, Stage3 + 14, "c > 0", ("c", "Float64"));
            Function note = Define("note", Function.AnyType, 42, Stage3 + 14,
                """
                # When, and the stations that heard it (in the order of the zip's ports: PFO, GSC, ISA, TIN).
                stations = ["PFO", "GSC", "ISA", "TIN"]
                h, m, s = round(Int, c) ÷ 3600, round(Int, c) % 3600 ÷ 60, round(Int, c) % 60
                heard_by = [stations[i] for i in eachindex(stations) if any(u -> u > 0 && abs(u - c) <= 60, (D[i], P[i]))]
                string(lpad(h, 2, '0'), ":", lpad(m, 2, '0'), ":", lpad(s, 2, '0'), " UTC, heard at ", join(heard_by, ", "))
                """,
                ("c", "Float64"), ("D", "Vector{Float64}"), ("P", "Vector{Float64}"));
            WhenNode onlyQuakes = new() { Row = 54, Column = Stage3 + 28 };
            DisplayNode shown = Shown(54, Stage3 + 44);
            SaveFile saved = new() { Path = System.IO.Path.Combine(RidgecrestNetwork.Folder, "quakes.csv"), PerFile = true, Row = 66, Column = Stage3 + 30 };

            // The stations and their zip come first, the rest a column further on.
            Node[] rest = [energies, levels, last, background, before, ratios, loudest, loud, shookBefore, shook, heard,
                heardBefore, noted, confirmed, kept, quake, note, onlyQuakes, shown, saved];
            foreach (Node node in rest)
            {
                node.Column += Stage2 - Stage1;
            }

            return Design(
                [.. stations, minutes, .. rest],
                [.. stations.Select((s, i) => ((Node)s, (Node)minutes, (object)i)),
                (minutes, energies, 0), (energies, levels, 0), (levels, last, 0), (last, background, 0), (background, before, "next"),
                (energies, ratios, 0), (before, ratios, 1), (ratios, loudest, 0), (loudest, loud, 0),
                (ratios, shook, 0), (shook, shookBefore, "next"), (ratios, heard, 0), (shookBefore, heard, 1), (name, heard, 2),
                (heard, confirmed, 0), (heardBefore, confirmed, 1), (noted, confirmed, 2), (heard, heardBefore, "next"),
                (confirmed, kept, 0), (noted, kept, 1), (kept, noted, "next"), (confirmed, quake, 0),
                (confirmed, note, 0), (heard, note, 1), (heardBefore, note, 2), (note, onlyQuakes, "value"), (quake, onlyQuakes, "condition"),
                (onlyQuakes, shown, 0), (onlyQuakes, saved, 0)]);
        }
    }
}
