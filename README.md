QFXView
======

Simple command-line utility to read QFX/OFX transaction files and print a concise summary of transactions.

Features

- Print a one-line summary for each transaction (type, date, amount, name, fit id).
- Optional detailed output of all parsed fields for each transaction.
- Optional date-range mode to print oldest and newest transaction dates.

Requirements

- .NET 10
- The project depends on System.CommandLine (declared in the project file).

Usage

  QFXView <file> [--detail|-d|/detail] [--range|-r|/range]

Parameters

- file (required): Path to the QFX/OFX/QFX file to process.
- --detail, -d, /detail (optional): Print all parsed fields for each transaction (detailed view).
- --range, -r, /range (optional): Print the date range (oldest and newest transaction) and exit.

Notes

- The --detail and --range options are mutually exclusive; specifying both will result in an error and a non-zero exit code.

Examples

```text
dotnet run -- transactions.qfx
dotnet run -- transactions.qfx --detail
dotnet run -- statement.ofx --range
```

Project

Files of interest:

- Program.cs: application entry point and parsing logic.
- QFXView.csproj: project file (includes System.CommandLine package).

License

MIT (as appropriate for your use)
