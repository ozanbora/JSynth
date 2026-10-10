using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JSynth.Model;
using JSynth.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JSynth.Tests.Streams
{
    /// <summary>
    /// A folder going through its files one at a time gives, besides each file's contents, its name and its path, on
    /// outputs of their own; a design saved with a File name node has its wires moved to them when opened.
    /// </summary>
    public sealed class FileOutputTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "JSynth.Tests", Guid.NewGuid().ToString("N"));

        public FileOutputTests()
        {
            _ = Directory.CreateDirectory(Path.Combine(_folder, "a"));
            _ = Directory.CreateDirectory(Path.Combine(_folder, "b"));
            File.WriteAllText(Path.Combine(_folder, "a", "1.csv"), "1\n");
            File.WriteAllText(Path.Combine(_folder, "b", "2.csv"), "2\n");
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        private static Function Shown(string name)
        {
            Function function = new() { Name = name, ReturnType = "String", Body = "s" };
            function.Parameters.Add(new Parameter("s", "String"));
            return function;
        }

        [Fact]
        public void A_folders_name_and_path_are_strings_set_only_when_wired_and_merged_each_folders_own()
        {
            Ui.Run(() =>
            {
                new MainViewModel().NewDesign();
                FilesNode a = new() { Folder = Path.Combine(_folder, "a"), EachFile = true };
                FilesNode b = new() { Folder = Path.Combine(_folder, "b"), Row = 20 };
                MergeNode merge = new();
                Function named = Shown("named");
                MainViewModel.Nodes.AddRange([a, b, merge, named]);
                Assert.Equal(("String", "String"), (a.OutputTypeOf(FilesNode.NameOutput), a.OutputTypeOf(FilesNode.PathOutput)));

                // Nothing wired from them: no name or path worked out.
                Assert.Null(MainViewModel.Connect(a, merge, merge.Folders));
                Assert.Null(MainViewModel.Connect(b, merge, merge.Folders));
                string plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                Assert.DoesNotContain("_name =", plain);

                // b's name: set as b reads its file, its latest kept as a's come.
                Assert.Null(MainViewModel.Connect(b, named, named.Parameters[0], FilesNode.NameOutput));
                plain = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: false).Text;
                Assert.Contains("    if _stream == 2\n        global frame2_name = basename(_path)\n        global frame2 = ", plain);
                Assert.Contains("    if @isdefined(frame2)\n        global named_out = named(frame2_name)", plain);
                string run = ScriptGenerator.Generate(MainViewModel.Nodes, MainViewModel.Connections, instrumented: true).Text;
                Assert.Contains("        try\n            global frame2_name = basename(_path)\n            global frame2 = ", run);
            });
        }

        [Fact]
        public void A_design_with_a_file_name_node_has_its_wires_moved_to_the_folders_outputs_when_opened()
        {
            string settingsBefore = AppSettings.SettingsPath;
            AppSettings.SettingsPath = Path.Combine(_folder, "settings.json");
            try
            {
                Ui.Run(() =>
                {
                    // As saved before: a File name node giving the whole path, and one the name.
                    new MainViewModel().NewDesign();
                    FilesNode files = new() { Folder = Path.Combine(_folder, "a"), EachFile = true };
                    FileNameNode path = new() { WholePath = true };
                    FileNameNode name = new();
                    Function byPath = Shown("by_path");
                    Function byName = Shown("by_name");
                    MainViewModel.Nodes.AddRange([files, path, name, byPath, byName]);
                    Assert.Null(MainViewModel.Connect(path, byPath, byPath.Parameters[0]));
                    Assert.Null(MainViewModel.Connect(name, byName, byName.Parameters[0]));
                    MainViewModel viewModel = new();
                    viewModel.SaveDesign(Path.Combine(_folder, "old.jsynth"));

                    viewModel.NewDesign();
                    viewModel.OpenDesign(Path.Combine(_folder, "old.jsynth"));
                    Assert.DoesNotContain(MainViewModel.Nodes, n => n is FileNameNode);
                    Assert.Equal(
                        [("by_name", FilesNode.NameOutput), ("by_path", FilesNode.PathOutput)],
                        MainViewModel.Connections.Where(c => c.SourceID == files.ID)
                            .Select(c => (((Function)MainViewModel.Nodes.Single(n => n.ID == c.TargetID)).Name, c.Output!))
                            .Order());
                });
            }
            finally
            {
                AppSettings.SettingsPath = settingsBefore;
            }
        }

        [Fact]
        public void The_name_and_path_ports_show_only_while_going_through_files_one_at_a_time()
        {
            Ui.Run(() =>
            {
                MainViewModel.LoadStartingDesign();
                MainViewModel viewModel = new();
                viewModel.NewDesign();
                FilesNode files = new() { Folder = Path.Combine(_folder, "a"), Row = 10, Column = 10 };
                MainViewModel.Nodes.Add(files);
                Window window = new() { Content = new Views.MainView(), DataContext = viewModel, Width = 1000, Height = 700 };
                window.Show();
                Dispatcher.UIThread.RunJobs();

                bool[] Shown() => [.. window.GetVisualDescendants().OfType<Controls.Connector>()
                    .Where(c => c.Output is FilesNode.NameOutput or FilesNode.PathOutput)
                    .Select(c => c.IsEffectivelyVisible)];
                Assert.Equal<bool[]>([false, false], Shown());
                files.EachFile = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal<bool[]>([true, true], Shown());
                window.Close();
            });
        }
    }
}
