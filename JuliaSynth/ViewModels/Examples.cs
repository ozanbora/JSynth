using JuliaSynth.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// A ready-made design showing what JuliaSynth is for.
    /// </summary>
    /// <param name="Name">Shown in the Examples menu.</param>
    /// <param name="Description">What it computes and what to try; shown when it is opened.</param>
    public sealed record Example(string Name, string Description, Func<MainViewModel.DesignDocument> Build)
    {
        /// <summary>
        /// Data it reads that is downloaded when it is opened, rather than bundled; null when there is none.
        /// </summary>
        public ExampleData? Data { get; init; }

        /// <summary>
        /// The field it is from, which its description starts with: "Finance".
        /// </summary>
        public string Category => Description.Split(':', 2) is [string field, _] ? field : string.Empty;

        /// <summary>
        /// What it works out: the first sentence of its description, after the field.
        /// </summary>
        public string Summary
        {
            get
            {
                string text = Description.Split(':', 2) is [_, string rest] ? rest.Trim() : Description;
                // Up to the end of the sentence, or of its first part when a colon goes on to the details.
                int end = new[] { text.IndexOf(". ", StringComparison.Ordinal), text.IndexOf(": ", StringComparison.Ordinal) }
                    .Where(i => i >= 0)
                    .DefaultIfEmpty(-1)
                    .Min();
                text = end < 0 ? text : text[..end] + ".";
                // "the monthly payment" → "The monthly payment"; "π estimated" stays as it is.
                return text.Length > 0 && char.IsAsciiLetterLower(text[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;
            }
        }
    }

    /// <summary>
    /// Real-world examples, built in code so they always match the current nodes. They run with Julia and
    /// its standard libraries, except that reading .mtx files needs MatrixMarket (offered for installing).
    /// Data they read is bundled in ExampleData, next to the app.
    /// </summary>
    public static class Examples
    {
        // Columns of the node centres: inputs, then stages of functions and displays (nodes are 9 cells wide).
        private const int Inputs = 8;
        private const int Stage1 = 22;
        private const int Stage2 = 36;
        private const int Stage3 = 50;

        /// <summary>
        /// The scans of the Boreas dataset's radar while its car stood still for 53 s, one every quarter second.
        /// </summary>
        private static readonly long[] StopScans =
        [
            1606417888039363, 1606417888289369, 1606417888539372, 1606417888789377, 1606417889039375, 1606417889290004,
            1606417889540012, 1606417889790012, 1606417890040020, 1606417890289392, 1606417890539398, 1606417890789406,
            1606417891039412, 1606417891289407, 1606417891539413, 1606417891789415, 1606417892040043, 1606417892290048,
            1606417892540051, 1606417892790064, 1606417893039433, 1606417893289440, 1606417893539442, 1606417893789448,
            1606417894039447, 1606417894289447, 1606417894539456, 1606417894790087, 1606417895039463, 1606417895289463,
            1606417895539466, 1606417895789474, 1606417896039477, 1606417896289487, 1606417896539483, 1606417896789487,
            1606417897039495, 1606417897289498, 1606417897538869, 1606417897789495, 1606417898039510, 1606417898290140,
            1606417898540140, 1606417898790762, 1606417899040761, 1606417899290776, 1606417899540154, 1606417899790155,
            1606417900040155, 1606417900290157, 1606417900540164, 1606417900790177, 1606417901039544, 1606417901289548,
            1606417901539558, 1606417901788937, 1606417902038936, 1606417902289561, 1606417902540190, 1606417902790201,
            1606417903040197, 1606417903290204, 1606417903539585, 1606417903789585, 1606417904038961, 1606417904288973,
            1606417904539598, 1606417904789597, 1606417905040227, 1606417905290857, 1606417905540239, 1606417905790244,
            1606417906040239, 1606417906290242, 1606417906539626, 1606417906789632, 1606417907039631, 1606417907289635,
            1606417907539640, 1606417907789646, 1606417908040302, 1606417908290895, 1606417908540277, 1606417908790288,
            1606417909040290, 1606417909290292, 1606417909540294, 1606417909790296, 1606417910040302, 1606417910289682,
            1606417910539683, 1606417910789688, 1606417911039688, 1606417911289070, 1606417911539072, 1606417911789701,
            1606417912039700, 1606417912290339, 1606417912540340, 1606417912790339, 1606417913040347, 1606417913290345,
            1606417913539726, 1606417913789728, 1606417914039738, 1606417914289743, 1606417914539739, 1606417914789741,
            1606417915039753, 1606417915289749, 1606417915539757, 1606417915789755, 1606417916039761, 1606417916289767,
            1606417916539767, 1606417916789161, 1606417917039769, 1606417917289782, 1606417917540410, 1606417917790409,
            1606417918040409, 1606417918290411, 1606417918539795, 1606417918789799, 1606417919039805, 1606417919289176,
            1606417919539808, 1606417919789817, 1606417920039820, 1606417920289828, 1606417920540451, 1606417920789824,
            1606417921039831, 1606417921289833, 1606417921539840, 1606417921789840, 1606417922039845, 1606417922289849,
            1606417922539853, 1606417922790486, 1606417923039856, 1606417923289872, 1606417923539868, 1606417923789875,
            1606417924040502, 1606417924290499, 1606417924540501, 1606417924789892, 1606417925039887, 1606417925289889,
            1606417925539892, 1606417925789901, 1606417926039904, 1606417926289912, 1606417926539909, 1606417926789911,
            1606417927039922, 1606417927289920, 1606417927539299, 1606417927789927, 1606417928039930, 1606417928290570,
            1606417928541187, 1606417928790568, 1606417929040569, 1606417929290571, 1606417929540581, 1606417929790582,
            1606417930040583, 1606417930289963, 1606417930539965, 1606417930789968, 1606417931039974, 1606417931289977,
            1606417931540605, 1606417931790605, 1606417932039983, 1606417932289991, 1606417932539994, 1606417932790009,
            1606417933040001, 1606417933290001, 1606417933540011, 1606417933790019, 1606417934040012, 1606417934290014,
            1606417934540026, 1606417934790023, 1606417935040030, 1606417935290030, 1606417935540034, 1606417935790046,
            1606417936040672, 1606417936290676, 1606417936540049, 1606417936790056, 1606417937040056, 1606417937290056,
            1606417937540692, 1606417937790696, 1606417938040079, 1606417938290072, 1606417938540076, 1606417938790079,
            1606417939040085, 1606417939290089, 1606417939540091, 1606417939790095, 1606417940040723, 1606417940290724,
            1606417940540731, 1606417940790118, 1606417941040743,        ];

        /// <summary>
        /// The Boreas radar scans of the stop: a short set of 12 spread over it (two of them with another
        /// radar's interference, scans 3 and 25 of the stop), or all 213.
        /// </summary>
        internal static ExampleData BoreasRadar { get; } = new(
            "Boreas radar scans",
            "Scans of a Navtech radar spinning on the roof of a car standing still for 53 seconds at a junction in Toronto, "
                + "on 26 November 2020, from the Boreas dataset. Each scan is a PNG image of 400 azimuths (rows) by 3,371 "
                + "columns: an 11-column timestamp, then 3,360 range bins of about 6 cm.",
            "From the Boreas dataset by the Autonomous Space Robotics Lab, University of Toronto, licensed under CC BY 4.0 "
                + "(https://creativecommons.org/licenses/by/4.0/): K. Burnett et al., \"Boreas: A multi-season autonomous "
                + "driving dataset\", The International Journal of Robotics Research, 2023. https://www.boreas.utias.utoronto.ca/ "
                + "The scans are unchanged.",
            "https://boreas.s3.us-west-2.amazonaws.com/boreas-2020-11-26-13-58/radar/",
            [
                new("Short",
                    "12 scans spread over the stop, two of them with another radar's interference: quick to download and run.",
                    [.. new[] { 1, 3, 20, 25, 40, 60, 80, 100, 120, 140, 160, 180 }.Select(i => $"{StopScans[i - 1]}.png")],
                    4_500_000),
                new("Full",
                    "All 213 scans of the stop, one every quarter second: traffic moves smoothly from scan to scan.",
                    [.. StopScans.Select(time => $"{time}.png")],
                    80_000_000),
            ]);

        /// <summary>
        /// The snapshots of the second IMS bearing test, named by the time: one every 10 minutes from 12 February
        /// 2004, 10:32:39, until bearing 1 failed on 19 February.
        /// </summary>
        private static readonly string[] BearingSnapshots = [.. Enumerable.Range(0, 984)
            .Select(i => new DateTime(2004, 2, 12, 10, 32, 39).AddMinutes(10 * i).ToString("yyyy.MM.dd.HH.mm.ss", System.Globalization.CultureInfo.InvariantCulture))];

        /// <summary>
        /// The second IMS bearing test, from NASA's zip of all three: every 8th snapshot and the last, or all 984.
        /// </summary>
        internal static ExampleData ImsBearings { get; } = new(
            "IMS bearings",
            "Vibration of four bearings on a shaft turning at 2,000 RPM under a 6,000 lb load until one of them failed: the "
                + "second test-to-failure of the IMS bearing data, 12 to 19 February 2004. Every 10 minutes, a 1-second snapshot "
                + "from an accelerometer on each bearing: 20,480 readings at 20 kHz, a column a bearing, in a text file named by "
                + "the time. Bearing 1's outer race failed at the end. The test's 86 MB archive is downloaded from NASA's zip of "
                + "all three tests, and unpacked.",
            "IMS bearing data from the NSF I/UCR Center for Intelligent Maintenance Systems (IMS), University of Cincinnati, "
                + "with support from Rexnord Corp.: J. Lee, H. Qiu, G. Yu, J. Lin, and Rexnord Technical Services (2007), "
                + "\"Bearing Data Set\", NASA Prognostics Data Repository, NASA Ames Research Center. A U.S. Government work "
                + "(public domain): https://data.nasa.gov/dataset/ims-bearings. Reference: H. Qiu, J. Lee, J. Lin, \"Wavelet "
                + "filter-based weak signature detection method and its application on roller bearing prognostics\", Journal "
                + "of Sound and Vibration 289 (2006) 1066-1090. The snapshots are unchanged.",
            "https://data.nasa.gov/docs/legacy/IMS.zip",
            [
                new("Short",
                    "Every 8th snapshot, one every 80 minutes, and the last: 69 MB once unpacked. Quick to run.",
                    [.. BearingSnapshots.Where((_, i) => i % 8 == 0), BearingSnapshots[^1]],
                    86_000_000),
                new("Full",
                    "All 984 snapshots, one every 10 minutes: 519 MB once unpacked.",
                    BearingSnapshots,
                    86_000_000),
            ])
        {
            Archive = new("https://data.nasa.gov/docs/legacy/IMS.zip", "IMS/2nd_test.rar"),
        };

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
        /// The frames of CDnet's highway video and their marked twins, by number: in000001.png and gt000001.png.
        /// </summary>
        private static string[] HighwayFrames(int count) => [.. Enumerable.Range(1, count).SelectMany(i => new[] { $"in{i:000000}.png", $"gt{i:000000}.png" })];

        /// <summary>
        /// The highway video of the CDnet 2014 change detection dataset, from its own archive: the frames turned to
        /// grey PNG as they are unpacked, with the frames marked by hand.
        /// </summary>
        internal static ExampleData CdnetHighway { get; } = new(
            "CDnet highway",
            "A fixed camera over a highway, 1,700 frames of 320×240, from the CDnet 2014 change detection dataset: each "
                + "frame comes with a twin marked by hand, white where something moves, black where nothing does (and grey "
                + "for shadows and what is unsure), to score a method by. Frames 470 to 1,700 are scored; the ones before "
                + "are for a method to learn the scene. The video's 28 MB archive is downloaded and unpacked, its JPEG "
                + "frames turned to grey PNG.",
            "From the CDnet 2014 change detection dataset (www.changedetection.net): Y. Wang, P.-M. Jodoin, F. Porikli, "
                + "J. Konrad, Y. Benezeth, P. Ishwar, \"CDnet 2014: An Expanded Change Detection Benchmark Dataset\", CVPR "
                + "Workshops 2014; the highway video is of its baseline category: N. Goyette, P.-M. Jodoin, F. Porikli, "
                + "J. Konrad, P. Ishwar, \"changedetection.net: A new change detection benchmark dataset\", CVPR Workshops "
                + "2012. The frames are turned from JPEG to grey PNG; those marked by hand are unchanged.",
            "https://changedetection.net/static/dataset/baseline/highway.zip",
            [
                new("Short",
                    "The first 800 frames: 469 to learn the scene, then 331 scored. 36 MB once unpacked.",
                    HighwayFrames(800),
                    28_000_000),
                new("Full",
                    "All 1,700 frames, 1,231 of them scored. 78 MB once unpacked.",
                    HighwayFrames(1700),
                    28_000_000),
            ])
        {
            Archive = new("https://changedetection.net/static/dataset/baseline/highway.zip", null)
            {
                // highway/input/in000001.jpg becomes in000001.png, a grey PNG; highway/groundtruth/gt000001.png stays.
                Rename = key => System.IO.Path.GetFileName(key) is string name && (key.Contains("/input/") || key.Contains("/groundtruth/"))
                    ? System.IO.Path.ChangeExtension(name, ".png")
                    : null,
                Convert = (key, from, to) =>
                {
                    if (key.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    {
                        ExampleArchive.GreyPng(from, to);
                    }
                    else
                    {
                        from.CopyTo(to);
                    }
                },
            },
        };

        public static IReadOnlyList<Example> All { get; } = [
            new("Loan payment",
                "Finance: the monthly payment on a 250,000 loan at 4.5% a year over 30 years, and the interest paid in total. "
                + "Change the amount, the rate or the years and run again (F5).",
                LoanPayment),
            new("Projectile",
                "Physics: how far and how high a ball thrown at 20 m/s and 45° flies, and for how long (no air resistance). "
                + "Try other angles: which one throws furthest?",
                Projectile),
            new("Line fit",
                "Statistics: the straight line best fitting test scores against hours studied (least squares), and the score "
                + "it predicts for 8 hours. Edit the data in the vectors.",
                LineFit),
            new("PageRank",
                "Web search: how important four linked web pages are, as Google first ranked them. Row i of the matrix has "
                + "a 1 for each page that page i links to; the damping is the chance of following a link.",
                PageRank),
            new("Heat in a rod",
                "Engineering: the steady temperature along a rod heated evenly and kept cold at both ends, from a sparse "
                + "matrix of 50 points: the plot shows the temperature along the rod. The hottest point should be close to 1/8, the exact answer.",
                HeatInARod),
            new("Monte Carlo π",
                "Simulation: π estimated from 100,000 random points in a square, by counting those inside the quarter circle. "
                + "The plot shows the points. More samples give a better estimate; run it a few times to see it vary.",
                MonteCarloPi),
            new("Weather station week",
                "Weather: a week of hourly temperatures, one CSV file a day, read one at a time. Map applies daily_mean "
                + "(which uses mean from Statistics) and daily_swing to every file; the plot shows the mean of each day "
                + "and warmest says which day was warmest. The files are in ExampleData/daily-temperatures, next to JuliaSynth: "
                + "point the Files node at your own folder of CSV files.",
                WeatherStationWeek),
            new("Network spread",
                "Networks: four small networks (a ring, a star, a path, and four nodes all linked) as sparse matrices in "
                + ".mtx files. Map counts each network's links and finds its spread rate, the largest eigenvalue "
                + "(LinearAlgebra), which says how fast a rumour or a virus spreads through it. Reading .mtx files "
                + "needs the MatrixMarket package: JuliaSynth offers to install it when the example opens.",
                NetworkSpread),
            new("Savings growth",
                "Finance: 10,000 saved at 5% a year, year by year for 30 years. Map applies balance to every year of "
                + "a range, without any files; the plot shows the growth and final the balance after 30 years. "
                + "Change the rate in balance and run again.",
                SavingsGrowth),
            new("Radar clutter map",
                "Radar: scans of a radar spinning on the roof of a car standing at a Toronto junction, from the Boreas "
                + "dataset (each a PNG image: azimuths by range bins), gone through one at a time. The inputs on the left keep "
                + "values from scan to scan: after each scan, each takes the value wired into its next port (the ring on its left), "
                + "as C = update(C, X) in a loop. The clutter map, the scene as it usually is, starts empty and moves a little "
                + "towards each scan. top_down shows it from above, like a radar screen: the streets of the junction dark, the "
                + "buildings, trees and parked cars on its corners bright. traffic shows, from above too, in how many scans each "
                + "spot stood out from the clutter: the tracks of the traffic along the streets. interfered finds the scans where "
                + "one azimuth stands out along most of its range, another radar's signal, and noted lists them: 2 and 4 of the "
                + "short set, 25 and 144 of the full one. range_profile gives the clutter's strength range bin by range bin. The "
                + "scans are downloaded when the example opens, 12 or all 213; reading PNG files needs PNGFiles and ColorTypes.",
                RadarClutterMap) { Data = BoreasRadar },
            new("Live radar",
                "Streaming: the radar scans of Radar clutter map, arriving one at a time as from the radar itself. "
                + "JuliaSynth replays them into a folder, 4 a second as the radar made them, and the Watch folder node "
                + "works through each as soon as it is written: inputs taking their next values keep the clutter map, count "
                + "where each scan stood out from it (the traffic) and the scans with another radar's interference, and the "
                + "plots follow live. Press Stop to end it. The scans are downloaded when the example opens; reading PNG files "
                + "needs PNGFiles and ColorTypes.",
                LiveRadar) { Data = BoreasRadar },
            new("Bearing wear",
                "Machines: the vibration of four bearings on a test rig run until one failed, a week of snapshots from NASA's IMS "
                + "bearing data, gone through one at a time. Snapshots of the rig stopped are passed over (only when running). "
                + "strength and spikiness give each bearing's RMS vibration and kurtosis, saved a row a snapshot to bearing-wear.csv "
                + "next to the data. A Window keeps the last 6 strengths, and smooth averages them: an hour of the full set, so one-off "
                + "spikes don't set the alarm off. learn takes each bearing's strength when new from the first 12 running snapshots; "
                + "wear is the most worn bearing's strength over it, plotted as it goes. The first time a bearing gets 10% stronger "
                + "than when new, the alarm notes when and which: bearing 1 on 16 February, 2.5 days before its outer race failed. "
                + "The snapshots are downloaded when the example opens, every 8th or all 984.",
                BearingWear) { Data = ImsBearings },
            new("Earthquake detector",
                "Seismology: a seismometer's recording, a file a minute, gone through as a station's data logger writes them: "
                + "station PFO in California on 4 July 2019, as the M6.4 Ridgecrest earthquake struck 200 km away. energy gives "
                + "each second's energy; a Window keeps the last 5 minutes' typical level, the background. ratio says how much "
                + "each second shook over it; 4 times is a quake, and a new one (the minute before had none) is noted with when and how "
                + "strong, and saved a row each to quakes.csv next to the data. In the hour from 16:40 UTC it notes the M4.0 foreshock "
                + "(17:03, 247 times the background), an M2.5 (17:10) and an M1.5 100 km away (17:27) as the USGS catalogue lists "
                + "them, a 4 times at 17:32 it doesn't, and the M6.4 (17:34); the 3 hours add 4 of its aftershocks. The plots show each minute's seismogram and the "
                + "loudest second of every minute. The files are downloaded when the example opens, the hour or 3 hours.",
                EarthquakeDetector) { Data = RidgecrestSeismograms },
            new("Motion in video",
                "Video: what moves in the frames of a fixed camera over a highway, from the CDnet 2014 change detection "
                + "dataset, gone through one at a time. As with the radar's clutter map, the background is the scene as it "
                + "usually is; moving marks the pixels far from it, and update makes it follow the scene, only where nothing "
                + "moves. Each frame's mask is saved as a picture (masks/mask-in000123.png next to the data) and scored against "
                + "the frame's twin marked by hand, found with the file path: f_score gives the precision, recall and F (1 is "
                + "perfect) of every frame so far: 0.82 on the first 800 frames, 0.84 on all. The frames are downloaded when the example opens, 800 or all "
                + "1,700; reading PNG files needs PNGFiles and ColorTypes.",
                MotionInVideo) { Data = CdnetHighway },
            new("Live air traffic",
                "Streaming: the aircraft over the Netherlands, Belgium and western Germany right now, from the OpenSky "
                + "Network's live web service. The Watch folder node fetches its address every 15 seconds and saves each answer "
                + "as a JSON file, named by the time; latest only means a slow run skips to the newest answer rather than falling "
                + "behind. airborne turns an answer into a row an aircraft (position, altitude, climb rate, speed), plotted as a "
                + "map; their count is saved a row a fetch to traffic.csv and plotted; steep lists those climbing or descending steeply. "
                + "Press Stop to end it. Without an account OpenSky answers 400 times a day: at one fetch every 15 seconds, about "
                + "an hour and 40 minutes. OpenSky's data is for research and other non-commercial use "
                + "(https://opensky-network.org); reading JSON needs the JSON package.",
                LiveAirTraffic),
        ];


        private static MainViewModel.DesignDocument LoanPayment()
        {
            Float amount = Scalar(250_000, 5);
            Float rate = Scalar(0.045, 13);
            Integer years = Whole(30, 21);

            Function payment = Define("payment", "Float64", 9, Stage1,
                """
                i = rate / 12        # monthly interest rate
                m = 12 * years       # number of payments
                P * i / (1 - (1 + i)^(-m))
                """,
                ("P", "Float64"), ("rate", "Float64"), ("years", "Int64"));

            Function interest = Define("interest", "Float64", 19, Stage2,
                "monthly * 12 * years - P",
                ("monthly", "Float64"), ("P", "Float64"), ("years", "Int64"));

            DisplayNode showPayment = Shown(6, Stage2);
            DisplayNode showInterest = Shown(19, Stage3);

            return Design([amount, rate, years, payment, interest, showPayment, showInterest],
                (payment, showPayment, 0), (interest, showInterest, 0),
                (amount, payment, 0), (rate, payment, 1), (years, payment, 2),
                (payment, interest, 0), (amount, interest, 1), (years, interest, 2));
        }

        private static MainViewModel.DesignDocument Projectile()
        {
            Float speed = Scalar(20, 9);
            Float angle = Scalar(45, 19);

            Function distance = Define("distance", "Float64", 4, Stage1, "v^2 * sind(2angle) / 9.81 # metres", ("v", "Float64"), ("angle", "Float64"));
            Function peak = Define("peak", "Float64", 14, Stage1, "(v * sind(angle))^2 / (2 * 9.81) # metres", ("v", "Float64"), ("angle", "Float64"));
            Function airtime = Define("airtime", "Float64", 24, Stage1, "2v * sind(angle) / 9.81 # seconds", ("v", "Float64"), ("angle", "Float64"));

            DisplayNode[] shown = [Shown(4, Stage2), Shown(14, Stage2), Shown(24, Stage2)];

            return Design([speed, angle, distance, peak, airtime, .. shown],
                (distance, shown[0], 0), (peak, shown[1], 0), (airtime, shown[2], 0),
                (speed, distance, 0), (angle, distance, 1),
                (speed, peak, 0), (angle, peak, 1),
                (speed, airtime, 0), (angle, airtime, 1));
        }

        private static MainViewModel.DesignDocument LineFit()
        {
            VectorNode hours = new() { Text = "[1, 2, 3, 4, 5, 6]" };
            VectorNode scores = new() { Text = "[52, 55, 61, 64, 70, 73]" };
            Float question = Scalar(8, 21);
            (hours.Row, hours.Column) = (5, Inputs);
            (scores.Row, scores.Column) = (13, Inputs);

            Function fit = Define("fit", "Vector{Float64}", 9, Stage1,
                """
                # Solve [x 1] * [slope, intercept] ≈ y in the least-squares sense.
                [x ones(length(x))] \ y
                """,
                ("x", "Vector{Float64}"), ("y", "Vector{Float64}"));

            Function predict = Define("predict", "Float64", 19, Stage2,
                "line[1] * hours + line[2]",
                ("line", "Vector{Float64}"), ("hours", "Float64"));

            DisplayNode showLine = Shown(6, Stage2);
            DisplayNode showScore = Shown(19, Stage3);

            return Design([hours, scores, question, fit, predict, showLine, showScore],
                (fit, showLine, 0), (predict, showScore, 0),
                (hours, fit, 0), (scores, fit, 1), (fit, predict, 0), (question, predict, 1));
        }

        private static MainViewModel.DesignDocument PageRank()
        {
            MatrixNode links = new() { Text = "[0 1 1 0; 0 0 1 0; 1 0 0 1; 0 0 1 0]", Row = 5, Column = Inputs };
            Float damping = Scalar(0.85, 13);

            Function pagerank = Define("pagerank", "Vector{Float64}", 9, Stage1,
                """
                n = size(links, 1)
                # Column j spreads page j's rank evenly over the pages it links to.
                M = links' ./ max.(sum(links, dims = 2)', 1)
                r = fill(1 / n, n)
                for _ in 1:100
                    r = (1 - d) / n .+ d .* (M * r)
                end
                r
                """,
                ("links", "Matrix{Float64}"), ("d", "Float64"));

            Function top = Define("top", "Int64", 19, Stage2, "argmax(rank) # the most important page", ("rank", "Vector{Float64}"));

            DisplayNode showRanks = Shown(6, Stage2);
            DisplayNode showTop = Shown(19, Stage3);

            return Design([links, damping, pagerank, top, showRanks, showTop],
                (pagerank, showRanks, 0), (top, showTop, 0),
                (links, pagerank, 0), (damping, pagerank, 1), (pagerank, top, 0));
        }

        private static MainViewModel.DesignDocument HeatInARod()
        {
            Integer points = Whole(50, 5);
            Float length = Scalar(1, 21);

            ArrayBuilder heat = new() { Kind = "fill", Row = 13, Column = Stage1 };

            Function laplacian = Define("laplacian", "SparseMatrixCSC{Float64, Int64}", 5, Stage1,
                """
                # Second differences between neighbouring points: 2 on the diagonal, -1 beside it.
                spdiagm(-1 => fill(-1.0, n - 1), 0 => fill(2.0, n), 1 => fill(-1.0, n - 1))
                """,
                ("n", "Int64"));

            Function temperature = Define("temperature", "Vector{Float64}", 9, Stage2,
                """
                h = L / (size(A, 1) + 1)   # distance between points
                h^2 * (A \ q)
                """,
                ("A", "AbstractMatrix{Float64}"), ("q", "Vector{Float64}"), ("L", "Float64"));

            Function hottest = Define("hottest", "Float64", 9, Stage3, "maximum(T)", ("T", "Vector{Float64}"));

            DisplayNode showHottest = Shown(9, Stage3 + 14);
            PlotNode curve = new() { Title = "Temperature along the rod", Row = 24, Column = Stage3 };

            return Design([points, length, heat, laplacian, temperature, hottest, showHottest, curve],
                (temperature, curve, "y"),
                (hottest, showHottest, 0),
                (points, laplacian, 0), (points, heat, "n"),
                (laplacian, temperature, 0), (heat, temperature, 1), (length, temperature, 2),
                (temperature, hottest, 0));
        }

        private static MainViewModel.DesignDocument MonteCarloPi()
        {
            Integer samples = Whole(100_000, 9);
            ArrayBuilder x = new() { Kind = "rand", Row = 5, Column = Stage1 };
            ArrayBuilder y = new() { Kind = "rand", Row = 13, Column = Stage1 };

            Function estimate = Define("estimate", "Float64", 9, Stage2,
                """
                # The share of points inside the quarter circle is π/4.
                4 * count(x .^ 2 .+ y .^ 2 .<= 1) / length(x)
                """,
                ("x", "Vector{Float64}"), ("y", "Vector{Float64}"));

            DisplayNode showEstimate = Shown(9, Stage3);
            PlotNode cloud = new() { Kind = "Scatter", Title = "Random points", Row = 24, Column = Stage2 };

            return Design([samples, x, y, estimate, showEstimate, cloud],
                (y, cloud, "y"), (x, cloud, "x"),
                (estimate, showEstimate, 0),
                (samples, x, "n"), (samples, y, "n"), (x, estimate, 0), (y, estimate, 1));
        }

        private static MainViewModel.DesignDocument WeatherStationWeek()
        {
            FilesNode days = new() { Folder = DataFolder("daily-temperatures"), Pattern = "day-*.csv", HasHeader = true, Row = 6, Column = Inputs };

            Function dailyMean = Define("daily_mean", "Float64", 16, Inputs, "mean(A[:, 2])  # column 2: °C", ("A", "Matrix{Float64}"));
            dailyMean.Packages = "Statistics";
            Function dailySwing = Define("daily_swing", "Float64", 26, Inputs,
                "maximum(A[:, 2]) - minimum(A[:, 2])  # warmest minus coldest hour",
                ("A", "Matrix{Float64}"));

            MapNode means = new() { Row = 11, Column = Stage1 };
            MapNode swings = new() { Row = 25, Column = Stage1 };
            PlotNode curve = new() { Title = "Mean temperature per day (°C)", Row = 9, Column = Stage2 + 3 };
            Function warmest = Define("warmest", "Int64", 22, Stage2, "argmax(means)  # the day of the week", ("means", "Vector{Float64}"));
            DisplayNode showWarmest = Shown(22, Stage3);
            DisplayNode showSwings = Shown(34, Stage2);

            return Design([days, dailyMean, dailySwing, means, swings, curve, warmest, showWarmest, showSwings],
                (days, means, "items"), (dailyMean, means, "f"),
                (days, swings, "items"), (dailySwing, swings, "f"),
                (means, curve, "y"), (means, warmest, 0), (warmest, showWarmest, 0),
                (swings, showSwings, 0));
        }

        private static MainViewModel.DesignDocument NetworkSpread()
        {
            FilesNode networks = new() { Format = FileFormats.MatrixMarket, Folder = DataFolder("networks"), Row = 6, Column = Inputs };

            Function links = Define("links", "Int64", 16, Inputs, "nnz(S) ÷ 2  # each link is stored both ways", ("S", "SparseMatrixCSC{Float64, Int64}"));
            Function spread = Define("spread", "Float64", 26, Inputs,
                """
                # The largest eigenvalue of the links: the faster things spread, the larger it is.
                maximum(abs, eigvals(Matrix(S)))
                """,
                ("S", "SparseMatrixCSC{Float64, Int64}"));
            spread.Packages = "LinearAlgebra";

            MapNode linkCounts = new() { Row = 11, Column = Stage1 };
            MapNode spreadRates = new() { Row = 25, Column = Stage1 };
            DisplayNode showLinks = Shown(11, Stage2);
            Function fastest = Define("fastest", "Int64", 25, Stage2, "argmax(rates)  # in file order: complete-4, path-5, ring-6, star-6", ("rates", "Vector{Float64}"));
            DisplayNode showFastest = Shown(25, Stage3);
            DisplayNode showRates = Shown(35, Stage2);

            return Design([networks, links, spread, linkCounts, spreadRates, showLinks, fastest, showFastest, showRates],
                (networks, linkCounts, "items"), (links, linkCounts, "f"),
                (networks, spreadRates, "items"), (spread, spreadRates, "f"),
                (linkCounts, showLinks, 0), (spreadRates, fastest, 0), (fastest, showFastest, 0),
                (spreadRates, showRates, 0));
        }

        private static MainViewModel.DesignDocument SavingsGrowth()
        {
            ArrayBuilder years = new() { Kind = "range", Row = 8, Column = Inputs };
            foreach ((string key, string value) in new[] { ("start", "0"), ("stop", "30"), ("n", "31") })
            {
                years.Parameters.OfType<Argument>().Single(a => a.Key == key).Value = value;
            }

            Function balance = Define("balance", "Float64", 18, Inputs, "10_000 * 1.05^t  # 10,000 saved at 5% a year, after t years", ("t", "Float64"));
            MapNode growth = new() { Row = 12, Column = Stage1 };
            PlotNode curve = new() { Title = "Balance by year", Row = 10, Column = Stage2 + 3 };
            Function final = Define("final", "Float64", 24, Stage2, "last(balances)", ("balances", "Vector{Float64}"));
            DisplayNode showFinal = Shown(24, Stage3);

            return Design([years, balance, growth, curve, final, showFinal],
                (years, growth, "items"), (balance, growth, "f"),
                (years, curve, "x"), (growth, curve, "y"), (growth, final, 0), (final, showFinal, 0));
        }

        /// <summary>
        /// A folder of bundled example data, next to the app (see ExampleData in the project).
        /// </summary>
        /// <summary>
        /// top_down(C) of the radar examples: a radar scan's azimuth-by-range matrix seen from above.
        /// </summary>
        private const string TopDownBody =
                """
                # The clutter map seen from above, like a radar screen: the radar in the middle, its 0° up,
                # 0.3 m a pixel out to 60 m. Each pixel takes the strongest echo of the range bins (6 cm) it covers.
                n, metres, bin = 401, 0.3, 0.0596
                centre = (n + 1) ÷ 2
                image = zeros(n, n)
                for i in 1:n, j in 1:n
                    x, y = (j - centre) * metres, (centre - i) * metres
                    r = hypot(x, y)
                    bins = max(floor(Int, (r - metres / 2) / bin) + 1, 1):min(ceil(Int, (r + metres / 2) / bin), size(C, 2))
                    isempty(bins) && continue
                    a = mod(round(Int, atan(x, y) / 2π * size(C, 1)), size(C, 1)) + 1  # the azimuth, clockwise from up
                    image[i, j] = maximum(view(C, a, bins))
                end
                image
                """;

        private const string UpdateBody =
                """
                # The clutter map, the scene as it usually is: each scan moves it a little towards itself.
                α = 0.2  # how quickly the clutter map follows the scene
                isempty(C) ? X : (1 - α) .* C .+ α .* X  # the first scan starts it
                """;

        private const string StandsOutBody =
                """
                # 1 where this scan stands out from the clutter map as it was before it, 0 elsewhere. C has this
                # scan in it already, at α = 0.2 (see update): X - C is 0.8 of how far X stands out from the map before.
                Float64.((X .- C) ./ 0.8 .> 0.15)
                """;

        private const string InterferedBody =
                """
                # 1 when an azimuth of this scan stands out along most of its range, in 8 of its 10 stretches
                # of 100 bins (6 m each): another radar's signal. A vehicle covers a few metres. As in stands_out,
                # against the clutter map before this scan.
                lit(a) = count(b -> count(>(0.15), (X[a, b:b+99] .- C[a, b:b+99]) ./ 0.8) >= 10, 1:100:1000)
                Int(any(a -> lit(a) >= 8, axes(X, 1)))
                """;

        /// <summary>
        /// An empty vector or matrix of <paramref name="elementType"/>: an input starting with nothing, which its
        /// next values fill.
        /// </summary>
        private static ArrayBuilder Empty(string shape, string elementType, int row)
        {
            ArrayBuilder empty = new() { Kind = "zeros", Shape = shape, ElementType = elementType, Row = row, Column = Inputs };
            foreach (Argument size in empty.Parameters.OfType<Argument>().Where(a => a.Key is "m" or "n"))
            {
                size.Value = "0";
            }

            return empty;
        }

        /// <summary>
        /// The radar scans gone through one at a time, from <paramref name="files"/> (a Files node so set, or a
        /// Watch folder): the clutter map, and what stands out from it, carried from scan to scan by inputs
        /// taking their next values. With <paramref name="list"/>, the scans with interference are listed,
        /// else counted; with <paramref name="profile"/>, the clutter's strength by range bin is plotted.
        /// </summary>
        private static MainViewModel.DesignDocument RadarScans(FilesNode files, string live, bool list, bool profile)
        {
            (files.Row, files.Column) = (6, Inputs);
            Function scan = Define("scan", "Matrix{Float64}", 6, Stage1,
                "X[:, 12:1011]  # without the 11 timestamp columns; the first 1000 range bins (about 60 m)",
                ("X", "Matrix{Float64}"));

            // The clutter map: empty at first, then each scan's update.
            ArrayBuilder clutter = Empty(JuliaTypes.Matrix, "Float64", 20);
            Function update = Define("update", "Matrix{Float64}", 20, Stage1, UpdateBody, ("C", "Matrix{Float64}"), ("X", "Matrix{Float64}"));
            Function topDown = Define("top_down", "Matrix{Float64}", 20, Stage2, TopDownBody, ("C", "Matrix{Float64}"));
            PlotNode clutterMap = new() { Title = $"Clutter map from above (0.3 m a pixel){live}", Kind = "Heatmap", Row = 21, Column = Stage3 + 2 };

            // How many scans each spot stood out in: empty at first, then each scan's count added.
            ArrayBuilder tally = Empty(JuliaTypes.Matrix, "Float64", 34);
            Function standsOut = Define("stands_out", "Matrix{Float64}", 34, Stage2, StandsOutBody, ("X", "Matrix{Float64}"), ("C", "Matrix{Float64}"));
            Function add = Define("add", "Matrix{Float64}", 34, Stage3, "isempty(T) ? S : T .+ S  # one more scan counted where a spot stood out",
                ("T", "Matrix{Float64}"), ("S", "Matrix{Float64}"));
            Function traffic = Define("traffic", "Matrix{Float64}", 34, Stage3 + 14,
                "top_down(min.(T, 3))  # from above, 3 scans or more brightest: what moves leaves tracks",
                ("T", "Matrix{Float64}"));
            PlotNode tracks = new() { Title = $"Standing out from the clutter, from above{live}", Kind = "Heatmap", Row = 35, Column = Stage3 + 30 };

            // Another radar's interference: 1 for a scan with it, counted, or listed by scan number.
            Function interfered = Define("interfered", "Int64", 48, Stage2, InterferedBody, ("X", "Matrix{Float64}"), ("C", "Matrix{Float64}"));

            List<Node> nodes = [files, scan, clutter, update, topDown, clutterMap, tally, standsOut, add, traffic, tracks, interfered];
            List<(Node, Node, object)> wires =
            [
                (files, scan, 0),
                (clutter, update, 0), (scan, update, 1), (update, clutter, "next"), (update, topDown, 0), (topDown, clutterMap, "y"),
                (scan, standsOut, 0), (update, standsOut, 1), (tally, add, 0), (standsOut, add, 1), (add, tally, "next"),
                (add, traffic, 0), (traffic, tracks, "y"),
                (scan, interfered, 0), (update, interfered, 1),
            ];

            if (list)
            {
                Integer number = Whole(0, 48);
                Function countUp = Define("count_up", "Int64", 48, Stage1, "k + 1  # the number of this scan", ("k", "Int64"));
                ArrayBuilder hits = Empty(JuliaTypes.Vector, "Int64", 60);
                Function noted = Define("noted", "Vector{Int64}", 55, Stage3,
                    "hit == 1 ? [hits; k] : hits  # the scans with another radar's interference, by number",
                    ("hits", "Vector{Int64}"), ("k", "Int64"), ("hit", "Int64"));
                DisplayNode shown = Shown(55, Stage3 + 16);
                nodes.AddRange([number, countUp, hits, noted, shown]);
                wires.AddRange([(number, countUp, 0), (countUp, number, "next"), (hits, noted, 0), (countUp, noted, 1), (interfered, noted, 2), (noted, hits, "next"), (noted, shown, 0)]);
            }
            else
            {
                Integer count = Whole(0, 48);
                Function plus = Define("plus", "Int64", 48, Stage3, "n + hit  # one more scan with interference, or not", ("n", "Int64"), ("hit", "Int64"));
                DisplayNode shown = Shown(48, Stage3 + 16);
                nodes.AddRange([count, plus, shown]);
                wires.AddRange([(count, plus, 0), (interfered, plus, 1), (plus, count, "next"), (plus, shown, 0)]);
            }

            if (profile)
            {
                Function rangeProfile = Define("range_profile", "Vector{Float64}", 6, Stage2,
                    "vec(sum(C; dims = 1)) ./ size(C, 1)  # the clutter's mean strength at each range bin",
                    ("C", "Matrix{Float64}"));
                PlotNode plot = new() { Title = "Clutter strength by range bin", Row = 7, Column = Stage3 + 2 };
                nodes.AddRange([rangeProfile, plot]);
                wires.AddRange([(update, rangeProfile, 0), (rangeProfile, plot, "y")]);
            }

            return Design([.. nodes], [.. wires]);
        }

        private static MainViewModel.DesignDocument RadarClutterMap()
        {
            // The full set when it was downloaded, else the short one, a scan at a time.
            string folder = BoreasRadar.DownloadedFolder ?? ExampleData.SetFolder(BoreasRadar.Folder, BoreasRadar.Sets[0]);
            return RadarScans(new FilesNode { Format = FileFormats.Png, Folder = folder, EachFile = true }, string.Empty, list: true, profile: true);
        }

        private static MainViewModel.DesignDocument LiveRadar()
        {
            // The scans are replayed, 4 a second as the radar made them, into a folder of their own that the
            // design watches: the full set when it was downloaded, else the short one.
            string live = System.IO.Path.Combine(BoreasRadar.Folder, "live");
            try
            {
                _ = System.IO.Directory.CreateDirectory(live);
            }
            catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
            {
                // The Watch folder node says the folder is missing.
            }

            WatchNode watch = new()
            {
                Format = FileFormats.Png,
                Folder = live,
                ReplayFolder = BoreasRadar.DownloadedFolder ?? ExampleData.SetFolder(BoreasRadar.Folder, BoreasRadar.Sets[0]),
                ReplayRate = 4,
            };

            return RadarScans(watch, ", live", list: false, profile: false);
        }

        /// <summary>
        /// The bearings' snapshots gone through one at a time by <paramref name="files"/>: each bearing's strength and
        /// spikiness saved to <paramref name="rows"/> a row a snapshot, its strength over the last 6 snapshots compared
        /// with its strength when new, and an alarm noting the first time one gets 10% stronger. Snapshots of a
        /// stopped rig are passed over.
        /// </summary>
        private static MainViewModel.DesignDocument BearingSnapshotsGoneThrough(FilesNode files, string rows)
        {
            (files.Row, files.Column) = (6, Inputs);
            Function running = Define("running", "Bool", 6, Stage1,
                "sqrt(sum(abs2, X) / length(X)) > 0.01  # a stopped rig reads close to 0",
                ("X", "Matrix{Float64}"));
            WhenNode whileRunning = new() { Row = 6, Column = Stage2 };

            // Each bearing's strength (RMS) and spikiness (kurtosis), saved a row a snapshot.
            Function strength = Define("strength", "Vector{Float64}", 6, Stage3,
                "vec(sqrt.(sum(abs2, X; dims = 1) ./ size(X, 1)))  # each bearing's RMS vibration",
                ("X", "Matrix{Float64}"));
            Function spikiness = Define("spikiness", "Vector{Float64}", 17, Stage3,
                """
                # Each bearing's kurtosis: 3 for smooth vibration, more as impacts (a defect rolled over) come.
                map(eachcol(X)) do x
                    d = x .- sum(x) / length(x)
                    length(x) * sum(d .^ 4) / sum(d .^ 2)^2
                end
                """,
                ("X", "Matrix{Float64}"));
            Function row = Define("row", "Vector{Float64}", 17, Stage3 + 14, "[r; k]  # strengths, then spikiness: bearings 1 to 4", ("r", "Vector{Float64}"), ("k", "Vector{Float64}"));
            SaveFile saved = new() { Path = rows, PerFile = true, Row = 17, Column = Stage3 + 28 };

            // The strength over the last snapshots, smoothing out one-off spikes.
            WindowNode last = new() { Size = 6, Row = 6, Column = Stage3 + 14 };
            Function smooth = Define("smooth", "Vector{Float64}", 6, Stage3 + 28, "sum(W) / length(W)  # each bearing's mean strength over the window", ("W", "Vector"));

            // The strength when new: the mean of the first 12 running snapshots, then kept.
            Integer seen = Whole(0, 29);
            Function countUp = Define("count_up", "Int64", 29, Stage1, "k + 1  # the number of this running snapshot", ("k", "Int64"));
            ArrayBuilder baseline = Empty(JuliaTypes.Vector, "Float64", 40);
            Function learn = Define("learn", "Vector{Float64}", 40, Stage3 + 14,
                """
                # Each bearing's strength when new: the mean of the first 12 running snapshots, then kept.
                k > 12 ? B : isempty(B) ? r : B .+ (r .- B) ./ k
                """,
                ("B", "Vector{Float64}"), ("k", "Int64"), ("r", "Vector{Float64}"));

            // Wear: the most worn bearing's strength over its strength when new, plotted snapshot by snapshot.
            Function wear = Define("wear", "Float64", 29, Stage3 + 42, "maximum(S ./ B)  # the most worn bearing: its strength now over when new", ("S", "Vector{Float64}"), ("B", "Vector{Float64}"));
            ArrayBuilder trend = Empty(JuliaTypes.Vector, "Float64", 52);
            Function grow = Define("grow", "Vector{Float64}", 52, Stage3 + 56, "[T; w]  # the wear of every snapshot so far", ("T", "Vector{Float64}"), ("w", "Float64"));
            PlotNode plot = new() { Title = "Wear: the most worn bearing's strength over when new", Kind = "Line", Row = 50, Column = Stage3 + 74 };

            // The alarm: 10% stronger than when new; the first one noted, with when and which bearing.
            Function alarm = Define("alarm", "Bool", 29, Stage3 + 56, "w > 1.1  # 10% stronger than when new", ("w", "Float64"));
            Integer alarms = Whole(0, 64);
            Function alarmCount = Define("alarm_count", "Int64", 64, Stage3 + 70, "c + a  # the alarms so far", ("c", "Int64"), ("a", "Bool"));
            Function firstAlarm = Define("first_alarm", "Bool", 29, Stage3 + 70, "a && c == 0  # the first alarm only", ("a", "Bool"), ("c", "Int64"));
            FileNameNode name = new() { Row = 17, Column = Stage3 + 42 };
            Function note = Define("note", Function.AnyType, 17, Stage3 + 56,
                """
                b = argmax(S ./ B)
                "$name: bearing $b, $(round(Int, 100 * (S[b] / B[b] - 1)))% stronger than when new"
                """,
                ("name", Function.AnyType), ("S", "Vector{Float64}"), ("B", "Vector{Float64}"));
            WhenNode noted = new() { Row = 17, Column = Stage3 + 70 };
            DisplayNode shown = Shown(17, Stage3 + 86);

            return Design(
                [files, running, whileRunning, strength, spikiness, row, saved, last, smooth, seen, countUp, baseline, learn,
                    wear, trend, grow, plot, alarm, alarms, alarmCount, firstAlarm, name, note, noted, shown],
                (files, running, 0), (files, whileRunning, "value"), (running, whileRunning, "condition"),
                (whileRunning, strength, 0), (whileRunning, spikiness, 0), (strength, row, 0), (spikiness, row, 1), (row, saved, 0),
                (strength, last, 0), (last, smooth, 0),
                (seen, countUp, 0), (countUp, seen, "next"), (baseline, learn, 0), (countUp, learn, 1), (strength, learn, 2), (learn, baseline, "next"),
                (smooth, wear, 0), (learn, wear, 1), (trend, grow, 0), (wear, grow, 1), (grow, trend, "next"), (grow, plot, "y"),
                (wear, alarm, 0), (alarms, alarmCount, 0), (alarm, alarmCount, 1), (alarmCount, alarms, "next"),
                (alarm, firstAlarm, 0), (alarms, firstAlarm, 1),
                (name, note, 0), (smooth, note, 1), (learn, note, 2), (note, noted, "value"), (firstAlarm, noted, "condition"), (noted, shown, 0));
        }

        private static MainViewModel.DesignDocument BearingWear()
        {
            // The full set when it was downloaded, else the short one, a snapshot at a time: 6 snapshots an hour,
            // or the short set's 6 (8 hours).
            string folder = ImsBearings.DownloadedFolder ?? ExampleData.SetFolder(ImsBearings.Folder, ImsBearings.Sets[0]);
            FilesNode files = new() { Format = FileFormats.Tsv, Pattern = "2004.*", Folder = folder, EachFile = true };
            return BearingSnapshotsGoneThrough(files, System.IO.Path.Combine(ImsBearings.Folder, "bearing-wear.csv"));
        }

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
            FileNameNode name = new() { Row = 53, Column = Stage3 + 28 };
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
                    name, note, noted, shown, saved, trend, grow, loudness],
                (files, seismogram, "y"), (files, energy, 0), (energy, level, 0), (level, last, 0), (last, background, 0),
                (energy, ratio, 0), (background, ratio, 1), (ratio, loudest, 0), (loudest, quake, 0),
                (quake, shook, 0), (shook, before, "next"), (quake, newQuake, 0), (before, newQuake, 1),
                (quakes, tally, 0), (newQuake, tally, 1), (tally, quakes, "next"),
                (name, note, 0), (ratio, note, 1), (note, noted, "value"), (newQuake, noted, "condition"), (noted, shown, 0), (noted, saved, 0),
                (trend, grow, 0), (loudest, grow, 1), (grow, trend, "next"), (grow, loudness, "y"));
        }

        private static MainViewModel.DesignDocument MotionInVideo()
        {
            // The full set when it was downloaded, else the short one, a frame at a time.
            string folder = CdnetHighway.DownloadedFolder ?? ExampleData.SetFolder(CdnetHighway.Folder, CdnetHighway.Sets[0]);
            FilesNode frames = new() { Format = FileFormats.Png, Pattern = "in*.png", Folder = folder, EachFile = true, Row = 6, Column = Inputs };

            // The background: the scene without what moves, following it slowly where nothing moves.
            ArrayBuilder background = Empty(JuliaTypes.Matrix, "Float64", 18);
            Function moving = Define("moving", "Matrix{Float64}", 6, Stage1,
                """
                # 1 where the frame is far from the background (something moves), 0 elsewhere: nothing on the first frame.
                isempty(B) ? zeros(size(X)) : Float64.(abs.(X .- B) .> 0.12)
                """,
                ("X", "Matrix{Float64}"), ("B", "Matrix{Float64}"));
            Function update = Define("update", "Matrix{Float64}", 18, Stage2,
                """
                # The background follows the scene, 5% a frame, but only where nothing moves: a passing car doesn't
                # smear into it. (Updating everywhere scores F 0.73 instead of 0.84: try it.)
                isempty(B) ? X : ifelse.(M .> 0, B, 0.95 .* B .+ 0.05 .* X)
                """,
                ("B", "Matrix{Float64}"), ("X", "Matrix{Float64}"), ("M", "Matrix{Float64}"));
            PlotNode frame = new() { Title = "The frame", Kind = "Heatmap", Row = 7, Column = Stage3 + 2 };
            PlotNode mask = new() { Title = "Moving: 1, still: 0", Kind = "Heatmap", Row = 7, Column = Stage3 + 20 };
            SaveFile saved = new() { Path = System.IO.Path.Combine(CdnetHighway.Folder, "masks", "mask.png"), PerFile = true, Row = 21, Column = Stage3 + 20 };

            // Scored against the frame's twin, marked by hand: pixels found moving that do, that don't, and those missed.
            FileNameNode path = new() { WholePath = true, Row = 32, Column = Inputs };
            Function twin = Define("twin", "Matrix{Float64}", 32, Stage1,
                """
                using PNGFiles
                import ColorTypes
                # The frame's twin marked by hand, in the same folder: in000123.png's gt000123.png. 1 moving, 0 still;
                # in between, shadows (0.2), outside the scored area (0.33) and unsure (0.67).
                Float64.(ColorTypes.gray.(ColorTypes.Gray.(PNGFiles.load(joinpath(dirname(p), "gt" * basename(p)[3:end])))))
                """,
                ("p", Function.AnyType));
            Function score = Define("score", "Vector{Float64}", 32, Stage2,
                """
                # Pixels found moving that move, found moving that are still, and moving but missed, from frame 470 on
                # (the highway video's scored frames: those before are for learning the scene).
                parse(Int, basename(p)[3:8]) < 470 && return zeros(3)
                found, moves, still = M .> 0, G .> 0.9, G .< 0.1
                Float64[count(found .& moves), count(found .& still), count(.!found .& moves)]
                """,
                ("M", "Matrix{Float64}"), ("G", "Matrix{Float64}"), ("p", Function.AnyType));
            ArrayBuilder totals = Empty(JuliaTypes.Vector, "Float64", 44);
            foreach (Argument size in totals.Parameters.OfType<Argument>().Where(a => a.Key == "n"))
            {
                size.Value = "3";
            }

            Function add = Define("add", "Vector{Float64}", 44, Stage3, "T .+ s  # the counts of every frame so far", ("T", "Vector{Float64}"), ("s", "Vector{Float64}"));
            Function fScore = Define("f_score", Function.AnyType, 44, Stage3 + 14,
                """
                # Precision (found moving that do), recall (moving found) and F, their harmonic mean: 1 is perfect.
                hits, wrong, missed = T
                p, r = hits / (hits + wrong), hits / (hits + missed)
                (precision = round(p; digits = 3), recall = round(r; digits = 3), F = round(2p * r / (p + r); digits = 3))
                """,
                ("T", "Vector{Float64}"));
            DisplayNode shown = Shown(44, Stage3 + 30);

            return Design(
                [frames, background, moving, update, frame, mask, saved, path, twin, score, totals, add, fScore, shown],
                (frames, moving, 0), (background, moving, 1), (background, update, 0), (frames, update, 1), (moving, update, 2), (update, background, "next"),
                (frames, frame, "y"), (moving, mask, "y"), (moving, saved, 0),
                (path, twin, 0), (moving, score, 0), (twin, score, 1), (path, score, 2), (totals, add, 0), (score, add, 1), (add, totals, "next"),
                (add, fScore, 0), (fScore, shown, 0));
        }

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
                # The aircraft climbing or descending more than 10 m/s (about 2,000 ft a minute): after take-off, or on the way down.
                flying = [s for s in something(get(J, "states", nothing), []) if s[12] !== nothing && abs(s[12]) > 10 && s[9] == false]
                isempty(flying) ? "None" : join(["$(strip(something(s[2], s[1]))) $(s[12] > 0 ? "↑" : "↓") $(round(Int, abs(s[12]))) m/s at $(round(Int, something(s[8], 0))) m" for s in flying], "\n")
                """,
                ("J", Function.AnyType));
            DisplayNode shown = Shown(42, Stage2 + 4);

            return Design(
                [watch, airborne, longitude, latitude, map, count, saved, trend, grow, counted, steep, shown],
                (watch, airborne, 0), (airborne, longitude, 0), (airborne, latitude, 0), (latitude, map, "y"), (longitude, map, "x"),
                (airborne, count, 0), (count, saved, 0), (trend, grow, 0), (count, grow, 1), (grow, trend, "next"), (grow, counted, "y"),
                (watch, steep, 0), (steep, shown, 0));
        }

        private static string DataFolder(string name)
        {
            return System.IO.Path.Combine(AppContext.BaseDirectory, "ExampleData", name);
        }

        private static Float Scalar(double value, int row)
        {
            // The bitness first: setting it resets the range.
            return new Float { SelectedType = "64-bit", Value = value, Row = row, Column = Inputs };
        }

        /// <summary>
        /// A Display node, to see a result on the canvas after a run.
        /// </summary>
        private static DisplayNode Shown(int row, int column)
        {
            return new DisplayNode { Row = row, Column = column };
        }

        private static Integer Whole(long value, int row)
        {
            return new Integer { SelectedType = "64-bit", Value = value, Row = row, Column = Inputs };
        }

        private static Function Define(string name, string returnType, int row, int column, string body, params (string Name, string Type)[] parameters)
        {
            Function function = new() { Name = name, ReturnType = returnType, Body = body, Row = row, Column = column };
            foreach ((string parameterName, string type) in parameters)
            {
                function.Parameters.Add(new Parameter(parameterName, type));
            }

            return function;
        }

        /// <summary>
        /// The nodes with their wires, each to an input given by position or by name; a wire the wiring
        /// rules refuse is a mistake in the example.
        /// </summary>
        private static MainViewModel.DesignDocument Design(Node[] nodes, params (Node Source, Node Target, object Input)[] wires)
        {
            MainViewModel.DesignDocument design = new();
            design.Nodes.AddRange(nodes);
            foreach ((Node source, Node target, object input) in wires)
            {
                Parameter parameter = input is int index ? target.Inputs[index] : input is "next" ? target.Next! : target.Inputs.Single(p => p.Name == (string)input);
                if (Wiring.Validate(design.Nodes, design.Connections, source, target, parameter) is string error)
                {
                    throw new InvalidOperationException($"Example wire to {parameter.Name}: {error}");
                }

                design.Connections.Add(new Connection(source.ID, target.ID, parameter.ID));
            }

            return design;
        }
    }
}
