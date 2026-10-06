using JuliaSynth.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// The storm surge of Hurricane Helene along Florida's Gulf coast: several streams merged.
    /// </summary>
    public static partial class Examples
    {
        /// <summary>
        /// The tide gauges along Florida's Gulf coast, south to north: their folders' names, NOAA station numbers and
        /// positions.
        /// </summary>
        private static (string Name, int Station, double Latitude, double Longitude)[] HeleneGauges =>
        [
            ("Key West", 8724580, 24.5557, -81.8079),
            ("Fort Myers", 8725520, 26.6478, -81.8711),
            ("St. Petersburg", 8726520, 27.7606, -82.6269),
            ("Clearwater Beach", 8726724, 27.9783, -82.8317),
            ("Cedar Key", 8727520, 29.135, -83.0317),
            ("Apalachicola", 8728690, 29.7244, -84.9806),
        ];

        /// <summary>
        /// The folder of the storm's positions, and of the gauges' tide tables.
        /// </summary>
        private const string HeleneTrack = "Helene";
        private const string HeleneTides = "Tides";

        /// <summary>
        /// Helene's best-track positions from 25 to 28 September 2024 (UTC): every 6 hours, and at landfall (03:10 on the
        /// 27th, near Perry) and its other notable moments.
        /// </summary>
        private static DateTime[] HeleneFixes => [.. new[]
        {
            "2024-09-25T00-00", "2024-09-25T06-00", "2024-09-25T12-00", "2024-09-25T18-00",
            "2024-09-26T00-00", "2024-09-26T06-00", "2024-09-26T12-00", "2024-09-26T18-00",
            "2024-09-27T00-00", "2024-09-27T03-10", "2024-09-27T05-00", "2024-09-27T06-00", "2024-09-27T09-00", "2024-09-27T12-00", "2024-09-27T18-00",
            "2024-09-28T00-00",
        }.Select(TimeOfName)];

        private static DateTime TimeOfName(string name) => DateTime.ParseExact(name, "yyyy-MM-dd'T'HH-mm", CultureInfo.InvariantCulture);

        private static string NameOfTime(DateTime time) => time.ToString("yyyy-MM-dd'T'HH-mm", CultureInfo.InvariantCulture);

        /// <summary>
        /// The 72 hours from 25 September 2024, 00:00 UTC, each named by the time it ends: 2024-09-25T01-00 for the
        /// readings from 00:00 to 00:54.
        /// </summary>
        private static IEnumerable<string> HeleneHours => Enumerable.Range(1, 72).Select(h => NameOfTime(new DateTime(2024, 9, 25).AddHours(h)));

        /// <summary>
        /// The six gauges' water levels during Hurricane Helene, a file an hour each, their tide tables, and the storm's
        /// track, a file a position. Made on first use (in a class of its own): this part of <see cref="Examples"/> may be
        /// set up after <see cref="All"/>, which takes it.
        /// </summary>
        internal static ExampleData HeleneSurge => HeleneData.Value;

        private static class HeleneData
        {
            internal static readonly ExampleData Value = new(
                "Hurricane Helene surge",
                "Hurricane Helene's storm surge along Florida's Gulf coast, 25 to 27 September 2024 (UTC): it came ashore near Perry "
                    + "at 03:10 on the 27th, a category 4. Six NOAA tide gauges, south to north: Key West, Fort Myers, St. Petersburg, "
                    + "Clearwater Beach, Cedar Key and Apalachicola, each in a folder of its own with a file an hour, named by the time "
                    + "it ends: the water level every 6 minutes (day of September, metres above mean lower low water). Their tide "
                    + "tables, in the folder Tides: the tide predicted for every 6 minutes of the 3 days. The storm's best track, in "
                    + "the folder Helene: a file a position (day of September, latitude, longitude, maximum wind in knots, pressure in "
                    + "hPa), named by its time.",
                "Water levels and tide predictions from NOAA's Center for Operational Oceanographic Products and Services (CO-OPS), "
                    + "Tides and Currents, https://tidesandcurrents.noaa.gov, through its data API; verified data. The track from the "
                    + "National Hurricane Center's Atlantic hurricane database HURDAT2 (Landsea, C. W. and J. L. Franklin, 2013: "
                    + "Atlantic Hurricane Database Uncertainty and Presentation of a New Database Format. Mon. Wea. Rev., 141, "
                    + "3576-3592), https://www.nhc.noaa.gov/data/#hurdat. U.S. Government works (public domain). The readings are "
                    + "unchanged; times are written as days of September 2024, and the hours with no reading left empty.",
                "https://api.tidesandcurrents.noaa.gov/api/prod/datagetter",
                [
                    new("All",
                        "The 3 days: 72 hours of each gauge, their tide tables and 16 of the storm's positions. 454 small files cut "
                            + "from 13 downloads: a few seconds.",
                        [
                            .. HeleneGauges.SelectMany(g => HeleneHours.Select(h => $"{g.Name}/{h}.csv")),
                            .. HeleneGauges.Select(g => $"{HeleneTides}/{g.Name}.csv"),
                            .. HeleneFixes.Select(t => $"{HeleneTrack}/{NameOfTime(t)}.csv"),
                        ],
                        400_000),
                ])
            {
                FileUrl = HeleneUrl,
                Convert = HeleneFile,
            };
        }

        /// <summary>
        /// HURDAT2, the Atlantic hurricane database, as of September 2026: Helene's track is in it.
        /// </summary>
        private const string Hurdat2 = "https://www.nhc.noaa.gov/data/hurdat/hurdat2-1851-2025-092326.txt";

        /// <summary>
        /// Where a file of the Helene data is: a gauge's 3 days of readings (each hour's file cut from them, as the
        /// CO-OPS API turns away hundreds of requests in a row) or tide table, or the hurricane database.
        /// </summary>
        private static string HeleneUrl(string name)
        {
            string folder = name.Split('/')[0];
            if (folder == HeleneTrack)
            {
                return Hurdat2;
            }

            static string Stamp(DateTime t) => t.ToString("yyyyMMdd'%20'HH':'mm", CultureInfo.InvariantCulture);
            (string product, int station) = folder == HeleneTides
                ? ("predictions&interval=6", HeleneGauges.Single(g => name == $"{HeleneTides}/{g.Name}.csv").Station)
                : ("water_level", HeleneGauges.Single(g => g.Name == folder).Station);
            return "https://api.tidesandcurrents.noaa.gov/api/prod/datagetter?application=JuliaSynth&datum=MLLW&time_zone=gmt&units=metric&format=csv"
                + $"&product={product}&station={station}&begin_date={Stamp(new DateTime(2024, 9, 25))}&end_date={Stamp(new DateTime(2024, 9, 27, 23, 54, 0))}";
        }

        /// <summary>
        /// The day of September 2024 a time is, with its fraction: 27.2 for 27 September, 04:48.
        /// </summary>
        private static string DayOfSeptember(DateTime time) =>
            (time.Day + time.TimeOfDay.TotalDays).ToString("0.#####", CultureInfo.InvariantCulture);

        /// <summary>
        /// Writes a downloaded file of the Helene data as it is kept: a gauge's readings or tide table as a day and a level
        /// a line (a CO-OPS CSV file's times and first value, the readings missing left out), or the storm's position at
        /// the file's time, from the hurricane database.
        /// </summary>
        /// <exception cref="IOException">The position isn't in the database.</exception>
        private static void HeleneFile(string name, Stream from, Stream to)
        {
            using StreamReader reader = new(from);
            using StreamWriter writer = new(to, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\n" };
            string folder = name.Split('/')[0];
            if (folder == HeleneTrack)
            {
                // The storm's lines follow its header line; a position line: date, time, record, status, latitude, longitude, wind, pressure, ...
                DateTime time = TimeOfName(name[(folder.Length + 1)..^".csv".Length]);
                string date = time.ToString("yyyyMMdd", CultureInfo.InvariantCulture), hour = time.ToString("HHmm", CultureInfo.InvariantCulture);
                bool helene = false;
                while (reader.ReadLine() is string line)
                {
                    string[] fields = [.. line.Split(',').Select(f => f.Trim())];
                    helene = fields[0].StartsWith("AL", StringComparison.Ordinal) ? fields[0] == "AL092024" : helene;
                    if (helene && fields[0] == date && fields[1] == hour)
                    {
                        static string Degrees(string value) => (value[^1] is 'S' or 'W' ? "-" : string.Empty) + value[..^1];
                        writer.WriteLine("day,latitude,longitude,wind,pressure");
                        writer.WriteLine($"{DayOfSeptember(time)},{Degrees(fields[4])},{Degrees(fields[5])},{fields[6]},{fields[7]}");
                        return;
                    }
                }

                throw new IOException($"Helene's position at {NameOfTime(time)} isn't in the hurricane database.");
            }

            // A gauge's hour: the readings of the hour its name ends.
            (DateTime start, DateTime until) = folder == HeleneTides ? (DateTime.MinValue, DateTime.MaxValue)
                : (TimeOfName(name[(folder.Length + 1)..^".csv".Length]).AddHours(-1), TimeOfName(name[(folder.Length + 1)..^".csv".Length]));
            writer.WriteLine(folder == HeleneTides ? "day,tide" : "day,level");
            while (reader.ReadLine() is string line)
            {
                string[] fields = line.Split(',');
                if (fields.Length > 1 && DateTime.TryParseExact(fields[0].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time)
                    && time >= start && time < until && fields[1].Trim().Length > 0)
                {
                    writer.WriteLine($"{DayOfSeptember(time)},{fields[1].Trim()}");
                }
            }
        }

        private static MainViewModel.DesignDocument HeleneSurgeMerged()
        {
            string folder = ExampleData.SetFolder(HeleneSurge.Folder, HeleneSurge.Sets[0]);
            FilesNode Folder(string name, int row, bool each = true) => new()
            {
                Format = FileFormats.Csv, Pattern = "*.csv", HasHeader = true, EachFile = each,
                Folder = System.IO.Path.Combine(folder, name), Row = row, Column = Inputs,
            };

            // The gauges' hours and the storm's positions, merged: in time order, as their names are.
            FilesNode[] gauges = [.. HeleneGauges.Select((g, i) => Folder(g.Name, 6 + (12 * i)))];
            FilesNode storm = Folder(HeleneTrack, 78);
            MergeNode merged = new() { Row = 6, Column = Stage1 };
            (Node, string) from = (merged, MergeNode.FolderOutput);
            Function isGauge = Define("is_gauge", "Bool", 30, Stage2, $"from != \"{HeleneTrack}\"  # a gauge's hour, not the storm's position", ("from", Function.AnyType));
            WhenNode hour = new() { Row = 6, Column = Stage2 };

            // The surge: the water level over the tide predicted for that minute.
            FilesNode tides = Folder(HeleneTides, 90, each: false);
            Function tables = Define("tide_tables", Function.AnyType, 90, Stage1,
                "Dict(splitext(basename(p))[1] => T.read(p) for p in T.paths)  # each gauge's predicted tide, by its name",
                ("T", Function.AnyType));
            Function surge = Define("surge", "Vector{Float64}", 6, Stage3,
                """
                # The hour's highest surge (m) and when: the water level over the tide predicted for that minute.
                P = tides[from]
                tide = Dict(round(Int, P[i, 1] * 1440) => P[i, 2] for i in axes(P, 1))
                s = [H[i, 2] - tide[round(Int, H[i, 1] * 1440)] for i in axes(H, 1)]
                isempty(s) ? [NaN, NaN] : [H[argmax(s), 1], maximum(s)]
                """,
                ("H", "Matrix{Float64}"), ("tides", Function.AnyType), ("from", Function.AnyType));

            // With the storm's latest position: how far it was, and on which side.
            string positions = string.Join(", ", HeleneGauges.Select(g => $"\"{g.Name}\" => ({g.Latitude.ToString(CultureInfo.InvariantCulture)}, {g.Longitude.ToString(CultureInfo.InvariantCulture)})"));
            Function row = Define("row", "Vector{Float64}", 6, Stage3 + 16,
                $$"""
                # The hour's row: when its highest surge was (day of September), the gauge (1 to 6, south to north), the surge
                # (m), how far the storm's latest position was (km), and 1 when it was to the gauge's west, else 0.
                gauges = [{{positions}}]
                g = findfirst(p -> first(p) == from, gauges)
                lat, lon = last(gauges[g])
                a, o = S[1, 2], S[1, 3]
                km = 6371 * 2asin(sqrt(sind((a - lat) / 2)^2 + cosd(lat) * cosd(a) * sind((o - lon) / 2)^2))
                [s[1], g, s[2], km, o < lon ? 1.0 : 0.0]
                """,
                ("s", "Vector{Float64}"), ("S", "Matrix{Float64}"), ("from", Function.AnyType));
            SaveFile saved = new() { Path = System.IO.Path.Combine(HeleneSurge.Folder, "surge.csv"), PerFile = true, Row = 20, Column = Stage3 + 30 };

            // Each gauge's highest surge so far, and every hour's, painted gauge by gauge.
            ArrayBuilder peaksSoFar = ZeroMatrix(6, 5, 42, Stage2);
            Function highest = Define("highest", "Matrix{Float64}", 42, Stage3 + 16,
                "r[3] > P[Int(r[2]), 3] ? [i == r[2] ? r[j] : P[i, j] for i in 1:6, j in 1:5] : P  # each gauge's row of its highest surge so far",
                ("P", "Matrix{Float64}"), ("r", "Vector{Float64}"));
            string names = string.Join(", ", HeleneGauges.Select(g => $"\"{g.Name}\""));
            Function note = Define("note", Function.AnyType, 42, Stage3 + 32,
                $$"""
                # Each gauge's highest surge, when, and where the storm was then.
                names = [{{names}}]
                when(d) = (m = round(Int, (d - floor(d)) * 1440); "$(floor(Int, d)) Sep $(lpad(m ÷ 60, 2, '0')):$(lpad(m % 60, 2, '0'))")
                join([P[g, 3] == 0 ? "$(names[g]): no surge yet" :
                    "$(names[g]): $(round(P[g, 3]; digits = 2)) m on $(when(P[g, 1])) UTC, the storm $(round(Int, P[g, 4])) km away to the $(P[g, 5] == 1 ? "west" : "east")"
                    for g in 1:6], "\n")
                """,
                ("P", "Matrix{Float64}"));
            DisplayNode shown = Shown(42, Stage3 + 48);
            ArrayBuilder hoursSoFar = ZeroMatrix(6, 72, 60, Stage2);
            Function paint = Define("paint", "Matrix{Float64}", 60, Stage3 + 16,
                "isnan(r[3]) ? M : [i == r[2] && j == floor(Int, (r[1] - 25) * 24) + 1 ? r[3] : M[i, j] for i in 1:6, j in 1:72]  # the hour's surge, at its gauge",
                ("M", "Matrix{Float64}"), ("r", "Vector{Float64}"));
            PlotNode heat = new() { Title = "Surge (m) hour by hour from 25 September, gauges south to north (Key West at the top)", Kind = "Heatmap", Row = 58, Column = Stage3 + 32 };

            return Design(
                [.. gauges, storm, merged, isGauge, hour, tides, tables, surge, row, saved, peaksSoFar, highest, note, shown, hoursSoFar, paint, heat],
                [
                    .. gauges.Append(storm).Select(g => ((Node)g, (Node)merged, (object)"folders")),
                    (from, isGauge, 0), (merged, hour, "value"), (isGauge, hour, "condition"),
                    (tides, tables, 0), (hour, surge, 0), (tables, surge, 1), (from, surge, 2),
                    (surge, row, 0), (storm, row, 1), (from, row, 2), (row, saved, 0),
                    (peaksSoFar, highest, 0), (row, highest, 1), (highest, peaksSoFar, "next"), (highest, note, 0), (note, shown, 0),
                    (hoursSoFar, paint, 0), (row, paint, 1), (paint, hoursSoFar, "next"), (paint, heat, "y"),
                ]);
        }

        /// <summary>
        /// A matrix of zeros, <paramref name="m"/> by <paramref name="n"/>: an input starting at nothing for each gauge.
        /// </summary>
        private static ArrayBuilder ZeroMatrix(int m, int n, int row, int column)
        {
            ArrayBuilder zeros = Empty(JuliaTypes.Matrix, "Float64", row);
            zeros.Column = column;
            foreach (Argument size in zeros.Parameters.OfType<Argument>().Where(a => a.Key is "m" or "n"))
            {
                size.Value = (size.Key == "m" ? m : n).ToString(CultureInfo.InvariantCulture);
            }

            return zeros;
        }
    }
}
