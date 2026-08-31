using Microsoft.Toolkit.Mvvm.Input;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            window.ResizeMode = ResizeMode.CanResize;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Width = 650;
            window.Height = 570;
            window.MinWidth = 500;
            window.MinHeight = 300;
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
            IncludeFirst = true;
            IncludeMiddle = true;
            IncludeLast = true;
            FirstLabel = "First Author";
            MiddleLabel = "Middle Author";
            LastLabel = "Last Author";
            AuthorSeparator = ",";

            SelectDirectoryCommand = new RelayCommand(SelectDirectory);
            SelectOutputFileCommand = new RelayCommand(SelectOutputFile);
            StartCommand = new AsyncRelayCommand(StartWork);
            StopCommand = new RelayCommand(Stop, () => State == State.Running);
        }

        private bool hasHeaderRow;
        private string outputFile;
        private bool _includeFirst, _includeMiddle, _includeLast;
        private bool _includeTotal, _includePercentages, _includeSummaryRow, _includeSoloCount;
        private string _firstLabel, _middleLabel, _lastLabel;
        private string _authorColumnHeader, _authorSeparator;
        private bool _namesAreLastFirst;
        private int _authorColumnIndex;

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
                    StopCommand?.NotifyCanExecuteChanged();
                });
            }
        }

        public RelayCommand SelectDirectoryCommand { get; set; }
        public RelayCommand SelectOutputFileCommand { get; set; }

        public AsyncRelayCommand StartCommand { get; set; }
        public RelayCommand StopCommand { get; set; }

        [DataMember]
        public string DirectoryPath { get; set; }

        [DataMember]
        public string OutputFilePath { get; set; }

        [DataMember]
        public bool HasHeader { get; set; }

        [DataMember]
        public bool IncludeFirst { get; set; }
        [DataMember]
        public bool IncludeMiddle { get; set; }
        [DataMember]
        public bool IncludeLast { get; set; }
        [DataMember]
        public string FirstLabel { get; set; }
        [DataMember]
        public string MiddleLabel { get; set; }
        [DataMember]
        public string LastLabel { get; set; }

        [DataMember]
        public bool IncludeTotal { get; set; }
        [DataMember]
        public bool IncludePercentages { get; set; }
        [DataMember]
        public bool IncludeSummaryRow { get; set; }
        [DataMember]
        public bool IncludeSoloCount { get; set; }

        [DataMember]
        public string AuthorColumnHeader { get; set; }
        [DataMember]
        public string AuthorSeparator { get; set; }
        [DataMember]
        public bool NamesAreLastFirst { get; set; }

        public int Progress { get; set; }
        public int TotalFiles { get; set; }

        #region Status Properties
        public string StatusText { get; set; } = "Status: Idle";
        public string StatusChecked { get; set; } = "Checked: 0";
        public string StatusFile { get; set; } = "File: -";
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

        private IEnumerable<string> GetAuthors(string[] lines)
        {
            var hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool firstRow = true;
            foreach (var line in lines)
            {
                if (hasHeaderRow && firstRow) { firstRow = false; continue; }
                firstRow = false;
                foreach (var author in GetAuthorsFromRow(line))
                    hs.Add(author);
            }
            return hs.OrderBy(a => a).ToList();
        }

        private IEnumerable<string> GetAuthorsFromRow(string line)
        {
            string cell;
            if (_authorColumnIndex >= 0)
            {
                var fields = ParseCsvRow(line);
                if (_authorColumnIndex >= fields.Count) yield break;
                cell = fields[_authorColumnIndex];
            }
            else
            {
                // Legacy: grab first quoted field
                cell = line.Substring("\"", "\"");
                if (cell is null) yield break;
            }

            foreach (var part in cell.Split(new[] { _authorSeparator }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed)) yield return trimmed;
            }
        }

        private static List<string> ParseCsvRow(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                }
            }
            fields.Add(sb.ToString());
            return fields;
        }

        private async Task StartWork()
        {
            string directoryPath = DirectoryPath;

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                MessageBox.Show("Please select an input directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(directoryPath))
            {
                MessageBox.Show("The selected directory does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputFilePath))
            {
                MessageBox.Show("Please select an output file path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var files = Directory.GetFiles(directoryPath, "*.csv", SearchOption.TopDirectoryOnly).ToList();

            if (files.Count == 0)
            {
                MessageBox.Show("No CSV files found in the selected directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            outputFile = OutputFilePath;
            hasHeaderRow = HasHeader;
            _includeFirst = IncludeFirst;
            _includeMiddle = IncludeMiddle;
            _includeLast = IncludeLast;
            _includeTotal = IncludeTotal;
            _includePercentages = IncludePercentages;
            _includeSummaryRow = IncludeSummaryRow;
            _includeSoloCount = IncludeSoloCount;
            _firstLabel = string.IsNullOrWhiteSpace(FirstLabel) ? "First Author" : FirstLabel;
            _middleLabel = string.IsNullOrWhiteSpace(MiddleLabel) ? "Middle Author" : MiddleLabel;
            _lastLabel = string.IsNullOrWhiteSpace(LastLabel) ? "Last Author" : LastLabel;
            _authorColumnHeader = AuthorColumnHeader;
            _authorSeparator = string.IsNullOrEmpty(AuthorSeparator) ? "," : AuthorSeparator;
            _namesAreLastFirst = NamesAreLastFirst;

            try
            {
                File.WriteAllText(outputFile, string.Empty);
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to write to the output file. Make sure it is not open in another program.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Resolve author column index from header row of the first file
            _authorColumnIndex = -1;
            if (!string.IsNullOrWhiteSpace(_authorColumnHeader) && hasHeaderRow)
            {
                var firstLine = File.ReadLines(files[0]).FirstOrDefault();
                if (firstLine != null)
                {
                    var headerFields = ParseCsvRow(firstLine);
                    _authorColumnIndex = headerFields.FindIndex(h =>
                        h.Trim().Equals(_authorColumnHeader.Trim(), StringComparison.OrdinalIgnoreCase));
                }
            }

            State = State.Running;
            Stats.Reset();
            Stats.TotalFiles = files.Count;
            TotalFiles = files.Count;
            Progress = 0;
            authorsResult = new Dictionary<string, AuthorData>();
            _ = StatusUpdater();

            int errorCount = 0;

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    if (State != State.Running)
                        break;

                    try
                    {
                        var fileInfo = new FileInfo(file);
                        Stats.CurrentFile = fileInfo.Name;

                        var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name).Replace("_", " ");
                        var fileLines = File.ReadAllLines(file);
                        var authors = GetAuthors(fileLines);
                        var authorsFiltered = new List<string>();

                        foreach (var author in authors)
                        {
                            if (AuthorMatchesTarget(author, fileName))
                                authorsFiltered.Add(author);
                        }

                        if (authorsFiltered.Count > 0)
                        {
                            string finalAuthor = authorsFiltered.OrderByDescending(s => s.Length).First();
                            foreach (var author in authorsFiltered)
                                CheckAuthor(fileLines, author, finalAuthor);
                        }
                        else
                        {
                            errorCount++;
                        }
                    }
                    catch (Exception)
                    {
                        errorCount++;
                    }

                    Stats.Checked += 1;
                    App.Current.Dispatcher.Invoke(() => Progress = Stats.Checked);
                }
            });

            SaveCSV();

            if (State == State.Stopping)
                State = State.Stopped;
            else
                State = State.Completed;

            var msg = State == State.Stopped ? "Task stopped." : "Task completed successfully.";
            if (errorCount > 0)
                msg += $"\n\n{errorCount} file(s) could not be processed (author name not matched in filename).";

            MessageBox.Show(msg,
                State == State.Stopped ? "Stopped" : "Success",
                MessageBoxButton.OK,
                State == State.Stopped ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private void CheckAuthor(string[] lines, string authorToFind, string finalAuthor)
        {
            int first = 0;
            int middle = 0;
            int last = 0;
            int solo = 0;

            bool firstRow = true;
            foreach (var line in lines)
            {
                if (hasHeaderRow && firstRow)
                {
                    firstRow = false;
                    continue;
                }

                var authorsList = GetAuthorsFromRow(line).ToList();

                if (authorsList.Count == 0)
                    continue;

                if (authorsList.Count == 1 && authorsList[0].Equals(authorToFind, StringComparison.OrdinalIgnoreCase))
                {
                    first += 1;
                    solo += 1;
                }
                else if (authorsList.First().Equals(authorToFind, StringComparison.OrdinalIgnoreCase))
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
            a.Solo += solo;
        }

        private Dictionary<string, AuthorData> authorsResult;

        private void SaveCSV()
        {
            var sb = new StringBuilder();

            // Build header row
            var headers = new List<string> { "Author Name" };
            if (_includeFirst) headers.Add(_firstLabel);
            if (_includeMiddle) headers.Add(_middleLabel);
            if (_includeLast) headers.Add(_lastLabel);
            if (_includeSoloCount) headers.Add("Solo Author");
            if (_includeTotal) headers.Add("Total");
            if (_includePercentages)
            {
                if (_includeFirst) headers.Add($"% {_firstLabel}");
                if (_includeMiddle) headers.Add($"% {_middleLabel}");
                if (_includeLast) headers.Add($"% {_lastLabel}");
            }
            sb.AppendLine(string.Join(",", headers));

            int grandFirst = 0, grandMiddle = 0, grandLast = 0, grandSolo = 0;

            foreach (var kvp in authorsResult)
            {
                var data = kvp.Value;
                int total = data.First + data.Middle + data.Last;
                grandFirst += data.First;
                grandMiddle += data.Middle;
                grandLast += data.Last;
                grandSolo += data.Solo;

                var row = new List<string> { EscapeCsvField(kvp.Key) };
                if (_includeFirst) row.Add(data.First.ToString());
                if (_includeMiddle) row.Add(data.Middle.ToString());
                if (_includeLast) row.Add(data.Last.ToString());
                if (_includeSoloCount) row.Add(data.Solo.ToString());
                if (_includeTotal) row.Add(total.ToString());
                if (_includePercentages)
                {
                    if (_includeFirst) row.Add(total > 0 ? $"{data.First * 100.0 / total:F1}" : "0.0");
                    if (_includeMiddle) row.Add(total > 0 ? $"{data.Middle * 100.0 / total:F1}" : "0.0");
                    if (_includeLast) row.Add(total > 0 ? $"{data.Last * 100.0 / total:F1}" : "0.0");
                }
                sb.AppendLine(string.Join(",", row));
            }

            if (_includeSummaryRow && authorsResult.Count > 0)
            {
                int authorCount = authorsResult.Count;
                int grandTotal = grandFirst + grandMiddle + grandLast;

                sb.AppendLine();
                var summaryHeaders = new List<string> { "SUMMARY" };
                var grandRow = new List<string> { "Grand Total" };
                var avgRow = new List<string> { "Average per Author" };

                if (_includeFirst) { summaryHeaders.Add(_firstLabel); grandRow.Add(grandFirst.ToString()); avgRow.Add($"{(double)grandFirst / authorCount:F2}"); }
                if (_includeMiddle) { summaryHeaders.Add(_middleLabel); grandRow.Add(grandMiddle.ToString()); avgRow.Add($"{(double)grandMiddle / authorCount:F2}"); }
                if (_includeLast) { summaryHeaders.Add(_lastLabel); grandRow.Add(grandLast.ToString()); avgRow.Add($"{(double)grandLast / authorCount:F2}"); }
                if (_includeSoloCount) { summaryHeaders.Add("Solo Author"); grandRow.Add(grandSolo.ToString()); avgRow.Add($"{(double)grandSolo / authorCount:F2}"); }
                if (_includeTotal) { summaryHeaders.Add("Total"); grandRow.Add(grandTotal.ToString()); avgRow.Add($"{(double)grandTotal / authorCount:F2}"); }

                sb.AppendLine(string.Join(",", summaryHeaders));
                sb.AppendLine(string.Join(",", grandRow));
                sb.AppendLine(string.Join(",", avgRow));

                var mostProlific = authorsResult.OrderByDescending(kvp => kvp.Value.First + kvp.Value.Middle + kvp.Value.Last).First();
                int mpTotal = mostProlific.Value.First + mostProlific.Value.Middle + mostProlific.Value.Last;
                sb.AppendLine($"Most Prolific,{EscapeCsvField(mostProlific.Key)},{mpTotal} publications");
            }

            File.AppendAllText(outputFile, sb.ToString());
        }

        private bool AuthorMatchesTarget(string csvAuthor, string targetFirstLast)
        {
            if (csvAuthor.Equals(targetFirstLast, StringComparison.OrdinalIgnoreCase))
                return true;

            ExtractNameParts(csvAuthor, _namesAreLastFirst, out string csvFirst, out string csvLast);
            ExtractNameParts(targetFirstLast, false, out string targetFirst, out string targetLast);

            if (string.IsNullOrEmpty(csvLast) || string.IsNullOrEmpty(targetLast))
                return false;

            if (!NamesEqual(csvLast, targetLast))
                return false;

            if (string.IsNullOrEmpty(csvFirst) || string.IsNullOrEmpty(targetFirst))
                return true;

            string cf = csvFirst.TrimEnd('.');
            string tf = targetFirst.TrimEnd('.');
            return NamesEqual(cf, tf) ||
                   (cf.Length == 1 && tf.StartsWith(cf, StringComparison.OrdinalIgnoreCase)) ||
                   (tf.Length == 1 && cf.StartsWith(tf, StringComparison.OrdinalIgnoreCase));
        }

        private static void ExtractNameParts(string name, bool isLastFirst, out string firstName, out string lastName)
        {
            name = name.Trim().Replace(".", " ");
            while (name.Contains("  ")) name = name.Replace("  ", " ");
            name = name.Trim();

            if (isLastFirst && name.Contains(","))
            {
                int comma = name.IndexOf(',');
                lastName = name.Substring(0, comma).Trim();
                var rest = name.Substring(comma + 1).Trim();
                var parts = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                firstName = parts.Length > 0 ? parts[0] : string.Empty;
            }
            else
            {
                var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                firstName = parts.Length > 0 ? parts[0] : string.Empty;
                lastName = parts.Length > 1 ? parts[parts.Length - 1] : string.Empty;
            }
        }

        private static bool NamesEqual(string a, string b)
        {
            if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
            return RemoveDiacritics(a).Equals(RemoveDiacritics(b), StringComparison.OrdinalIgnoreCase);
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }

        private void Stop()
        {
            var d = MessageBox.Show("Are you sure? This will stop the current task.", "Stop", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                    if (State == State.Stopped || State == State.Completed)
                        break;

                    await Task.Delay(TimeSpan.FromSeconds(1));
                    UpdateStatus();
                }

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
