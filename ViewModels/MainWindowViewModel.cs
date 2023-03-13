using Microsoft.Toolkit.Mvvm.Input;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Research_Author_Publication_Data
{
    [DataContract]
    public class MainWindowViewModel : BaseViewModel
    {
        public static MainWindowViewModel ViewModel;
        private const string SettingsFilePath = "Settings.json";

        public static void InitVM(MainWindow window)
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                ViewModel = JsonConvert.DeserializeObject<MainWindowViewModel>(json);
            }
            else
                ViewModel = new MainWindowViewModel();

            #region Window Properties
            window.DataContext = ViewModel;
            window.Title = "Research Author Publication Data";
            window.ResizeMode = ResizeMode.CanMinimize;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Width = 650;
            window.Height = 300;
            window.Closing += Window_Closing;
            #endregion
        }

        private static void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Save();
        }

        public static void Save()
        {
            var json = JsonConvert.SerializeObject(ViewModel, Formatting.Indented);
            File.WriteAllText(SettingsFilePath, json);
        }

        public MainWindowViewModel()
        {
            SelectDirectoryCommand = new RelayCommand(SelectDirectory);
            SelectOutputFileCommand = new RelayCommand(SelectOutputFile);

            StartCommand = new RelayCommand(Start, () => State == State.Completed || State == State.Idle || State == State.Stopped);
            StopCommand = new RelayCommand(Stop, () => State == State.Running);
        }


        private bool hasHeaderRow;
        private string outputFile;

        private State _state;
        public State State
        {
            get => _state;
            set
            {
                if (_state == value)
                    return;

                _state = value;
                App.Current.Dispatcher.Invoke(() =>
                {
                    StartCommand?.NotifyCanExecuteChanged();
                    StopCommand?.NotifyCanExecuteChanged();
                });
            }
        }

        public RelayCommand SelectDirectoryCommand { get; set; }
        public RelayCommand SelectOutputFileCommand { get; set; }

        public RelayCommand StartCommand { get; set; }
        public RelayCommand StopCommand { get; set; }

        public int SelectedPageIndex { get; set; }

        [DataMember]
        public string DirectoryPath { get; set; }

        [DataMember]
        public string OutputFilePath { get; set; }

        [DataMember]
        public bool HasHeader { get; set; }

        #region Status Properties
        public string StatusText { get; set; } = $"Status: Idle";
        public string StatusChecked { get; set; } = $"Checked: 0";
        public string StatusFile { get; set; } = $"File: -";
        #endregion

        private void SelectOutputFile()
        {
            if (Helper.SaveFileDialog("Select Output File", out string fileName))
                OutputFilePath = fileName;
        }

        private void SelectDirectory()
        {
            if (Helper.OpenFolderDialog("Select Directory", out string folderName))
                DirectoryPath = folderName;
        }

        public void Start()
        {
            StartWork();
        }

        private IEnumerable<string> GetAuthors(string[] lines)
        {
            var hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                var authors = line.Substring("\"", "\"");
                if (authors is null)
                    continue;

                var authorsList = authors.Split(new String[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
                authorsList.ForEach(author => hs.Add(author.Trim()));
            }

            return hs.OrderBy(a => a).ToList();
        }

        private void StartWork()
        {
            string directoryPath = DirectoryPath;

            if (!Directory.Exists(directoryPath))
            {
                MessageBox.Show("Invalid Directory Path Selected!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var files = Directory.GetFiles(directoryPath, "*.csv", SearchOption.TopDirectoryOnly).ToList();

            if (files.Count == 0)
            {
                MessageBox.Show("No Files found in selected directory!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            outputFile = OutputFilePath;
            hasHeaderRow = HasHeader;

            try
            {
                File.WriteAllText(outputFile, String.Empty);
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to Read/Write output file. Make sure it is not opened in any program!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            State = State.Running;

            Stats.Reset();

            Stats.TotalFiles = files.Count;
            authorsResult = new Dictionary<string, AuthorData>();
            StatusUpdater();
            isFirstRow = true;

            foreach (var file in files)
            {
                if (State != State.Running)
                    break;

                try
                {
                    var fileInfo = new FileInfo(file);
                    Stats.CurrentFile = fileInfo.Name;

                    var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name)
                        .Replace("_", " ");

                    var fileLines = File.ReadAllLines(file);
                    var authors = GetAuthors(fileLines);
                    var authorsFiltered = new List<string>();

                    foreach (var author in authors)
                    {
                        string _author = author.Replace(".", " ");

                        if (fileName.StartsWith(_author.Trim(), StringComparison.OrdinalIgnoreCase))
                            authorsFiltered.Add(author);
                    }

                    string finalAuthor = authorsFiltered.OrderByDescending(s => s.Length).First();

                    foreach (var author in authorsFiltered)
                    {
                        CheckAuthor(fileLines, author, finalAuthor);
                    }
                }
                catch (Exception)
                {
                }

                Stats.Checked += 1;
            }

            SaveCSV();

            if (State is State.Stopping)
                State = State.Stopped;
            else
                State = State.Completed;

            var msg = "Task " + (State is State.Stopped ? "Stopped Successfully" : "Successfully Completed");
            MessageBox.Show(msg, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CheckAuthor(string[] lines, string authorToFind, string finalAuthor)
        {
            int first = 0;
            int middle = 0;
            int last = 0;

            bool firstRow = true;
            foreach (var line in lines)
            {
                if (hasHeaderRow && firstRow)
                {
                    firstRow = false;
                    continue;
                }

                var authors = line.Substring("\"", "\"");
                if (authors is null)
                    continue;

                var authorsList = authors.Split(new String[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim()).ToList();

                if (authorsList.Count == 0)
                    continue;

                if (authorsList.First().Equals(authorToFind, StringComparison.OrdinalIgnoreCase))
                    first += 1;
                else if (authorsList.Last().Equals(authorToFind, StringComparison.OrdinalIgnoreCase))
                    last += 1;
                else if (authorsList.Contains(authorToFind, StringComparer.OrdinalIgnoreCase))
                    middle += 1;
            }

            if (!authorsResult.ContainsKey(finalAuthor))
                authorsResult.Add(finalAuthor, new AuthorData());

            var a = authorsResult[finalAuthor];
            a.First += first;
            a.Middle += middle;
            a.Last += last;
        }

        private bool isFirstRow = true;
        private Dictionary<string, AuthorData> authorsResult;

        private void SaveCSV()
        {
            var sb = new StringBuilder();

            if (isFirstRow)
            {
                sb.Append($"Author Name,");
                sb.Append($"First Author,");
                sb.Append($"Middle Author,");
                sb.Append($"Last Author");
                sb.AppendLine();

                isFirstRow = false;
            }

            foreach (var author in authorsResult)
            {
                sb.Append($"{author.Key},");
                sb.Append($"{author.Value.First},");
                sb.Append($"{author.Value.Middle},");
                sb.Append($"{author.Value.Last}");
                sb.AppendLine();
            }


            File.AppendAllText(outputFile, sb.ToString());
        }

        private void Stop()
        {
            var d = MessageBox.Show("Are you sure? This will stop the current task!", "Stop", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (d != MessageBoxResult.Yes)
                return;

            State = State.Stopping;
        }

        #region Status
        private Task StatusUpdater()
        {
            return Task.Run(async () =>
            {
                while (true)
                {
                    if (State is State.Stopped ||
                        State is State.Completed)
                        break;

                    await Task.Delay(TimeSpan.FromSeconds(1));
                    UpdateStatus();
                }

                //Status: Complete
                UpdateStatus();
            });
        }

        private void UpdateStatus()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                StatusText = $"Status: {Enum.GetName(typeof(State), State)}";
                StatusChecked = $"Checked: {Stats.Checked:N0}/{Stats.TotalFiles:N0}";
                StatusFile = $"File: {Stats.CurrentFile}";
            });
        }
        #endregion
    }
}
