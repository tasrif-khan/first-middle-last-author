namespace Research_Author_Publication_Data
{
    public class Stats
    {
        public static int TotalFiles;
        public static int Checked;
        public static string CurrentFile;

        public static void Reset()
        {
            Checked = 0;
            TotalFiles = 0;
            CurrentFile = string.Empty;
        }
    }
}
