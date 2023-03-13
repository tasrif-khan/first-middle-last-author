using Microsoft.Win32;

namespace Research_Author_Publication_Data
{
    public class Helper
    {
        public static bool OpenFolderDialog(string title, out string path)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
            {
                Description = title
            };

            if (dialog.ShowDialog().GetValueOrDefault())
            {
                path = dialog.SelectedPath;
                return true;
            }


            path = string.Empty;
            return false;
        }

        public static bool SaveFileDialog(string title, out string fileName)
        {
            SaveFileDialog ofd = new SaveFileDialog
            {
                Title = title,
                DefaultExt = "csv",
                Filter = "Csv files (*.csv)|*.csv|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            var resp = ofd.ShowDialog();
            if (resp.GetValueOrDefault())
            {
                fileName = ofd.FileName;
                return true;
            }

            fileName = string.Empty;
            return false;
        }
    }
}
