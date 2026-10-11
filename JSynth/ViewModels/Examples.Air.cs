using JSynth.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JSynth.ViewModels
{
    /// <summary>
    /// The live air traffic example: OpenSky's aircraft, fetched again and again.
    /// </summary>
    public static partial class Examples
    {
        /// <summary>
        /// OpenSky's live aircraft over the Netherlands, Belgium and western Germany: latitude 50.5° to 52.5° N,
        /// longitude 3.5° to 7.5° E, an area OpenSky counts as one request.
        /// </summary>
        internal const string OpenSkyUrl = "https://opensky-network.org/api/states/all?lamin=50.5&lomin=3.5&lamax=52.5&lomax=7.5";

        private static MainViewModel.DesignDocument LiveAirTraffic()
        {
            // OpenSky's answers are saved here as they come, one file a fetch, named by the time: the folder is made when
            // the stream starts.
            string live = ExampleData.FolderOf("OpenSky aircraft");

            WatchNode watch = new()
            {
                Format = FileFormats.Json, Folder = live, ExistingFirst = false, LatestOnly = true,
                FetchUrl = OpenSkyUrl, FetchEvery = 15, Row = 6, Column = Inputs,
            };

            Function airborne = Define("airborne", "Matrix{Float64}", 6, Stage1,
                """
                # The aircraft in the air, a row each: longitude, latitude, altitude (m), climb rate (m/s), speed (m/s).
                # OpenSky gives each as a list: [icao24, callsign, country, time, last contact, longitude, latitude,
                # barometric altitude, on ground, speed, heading, climb rate, ...], nothing where it doesn't know.
                flying = [s for s in something(get(J, "states", nothing), []) if s[6] !== nothing && s[7] !== nothing && s[9] == false]
                isempty(flying) ? zeros(0, 5) : permutedims(reduce(hcat, [Float64[s[6], s[7], something(s[8], 0), something(s[12], 0), something(s[10], 0)] for s in flying]))
                """,
                ("J", Function.AnyType));
            Function longitude = Define("longitude", "Vector{Float64}", 6, Stage2, "A[:, 1]", ("A", "Matrix{Float64}"));
            Function latitude = Define("latitude", "Vector{Float64}", 16, Stage2, "A[:, 2]", ("A", "Matrix{Float64}"));
            PlotNode map = new() { Title = "Aircraft over the Netherlands and Belgium (longitude, latitude), live", Kind = "Scatter", Row = 12, Column = Stage3 + 8 };

            // How many, fetch by fetch: saved a row each, and plotted.
            Function count = Define("aircraft", "Int64", 28, Stage1, "size(A, 1)  # how many are in the air over the area", ("A", "Matrix{Float64}"));
            SaveFile saved = new() { Path = System.IO.Path.Combine(live, "traffic.csv"), PerFile = true, Row = 28, Column = Stage2 };
            ArrayBuilder trend = Empty(JuliaTypes.Vector, "Float64", 28);
            Function grow = Define("grow", "Vector{Float64}", 28, Stage3, "[T; n]  # the count of every fetch so far", ("T", "Vector{Float64}"), ("n", "Int64"));
            PlotNode counted = new() { Title = "Aircraft in the air, fetch by fetch", Kind = "Line", Row = 34, Column = Stage3 + 22 };

            // Who climbs or descends steeply now.
            Function steep = Define("steep", Function.AnyType, 42, Stage1,
                """
                # The aircraft climbing or descending faster than v m/s (10: about 2,000 ft a minute): after take-off, or on the way down.
                flying = [s for s in something(get(J, "states", nothing), []) if s[12] !== nothing && abs(s[12]) > v && s[9] == false]
                isempty(flying) ? "None" : join(["$(strip(something(s[2], s[1]))) $(s[12] > 0 ? "↑" : "↓") $(round(Int, abs(s[12]))) m/s at $(round(Int, something(s[8], 0))) m" for s in flying], "\n")
                """,
                ("J", Function.AnyType), ("v", "Float64"));
            DisplayNode shown = Shown(42, Stage2 + 4);
            Float faster = InPanel(Scalar(10, 52), "steep: faster than (m/s)", 2, 30);

            FrameNode where = Framed("Where they are", "airborne: a row an aircraft in the air; the map plots their longitude and latitude.",
                airborne, longitude, latitude, map);
            FrameNode howMany = Framed("How many", "Counted fetch by fetch: saved a row each to traffic.csv, and plotted.",
                count, saved, trend, grow, counted);
            FrameNode climbing = Framed("Climbing or descending steeply", "Faster than the slider on the left: after take-off, or on the way down.",
                faster, steep, shown);

            return Design(
                [watch, where, howMany, climbing, airborne, longitude, latitude, map, count, saved, trend, grow, counted, faster, steep, shown],
                (watch, airborne, 0), (airborne, longitude, 0), (airborne, latitude, 0), (latitude, map, "y"), (longitude, map, "x"),
                (airborne, count, 0), (count, saved, 0), (trend, grow, 0), (count, grow, 1), (grow, trend, "next"), (grow, counted, "y"),
                (watch, steep, 0), (faster, steep, 1), (steep, shown, 0));
        }
    }
}
