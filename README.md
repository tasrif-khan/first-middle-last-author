# first-middle-last-author

A WPF desktop tool that tallies how many times a researcher appears as first, middle, or last author across their Scopus publication history.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build from source)

## Build & Run

```
dotnet run
```

Or open the `.csproj` in Visual Studio 2022+ and press **F5**.

To produce a standalone executable:

```
dotnet publish -c Release -r win-x64 --self-contained true
```

The output will be in `bin\Release\net8.0-windows\win-x64\publish\`.

## Usage

1. Export an author's publication list from [Scopus](https://www.scopus.com/) as CSV, selecting only the **Authors** and **Title** columns (or at minimum, the Authors column wrapped in quotes).
2. Name each CSV file after the author: `LastName FirstInitial MiddleInitial.csv`
   - Example: `Smith J D.csv` or `Smith J.csv` (omit middle initial if none)
   - Underscores in the filename are treated as spaces.
3. Place all CSV files in a single folder.
4. Run the program:
   - Select the **input folder** containing the CSV files.
   - Select the **output CSV file** where results will be saved.
   - Check **Has Header Row** if your exported CSV includes a header.
   - Click **Start**.
5. The output CSV will contain columns: `Author Name`, `First Author`, `Middle Author`, `Last Author`.
