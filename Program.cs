using System.CommandLine;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QFXView
{
	internal class Program
	{
		/// <summary>
		/// Application entry point.
		/// </summary>
		/// <param name="args">Command-line arguments.
		/// Expected usage: QFXView &lt;file&gt; [--detail|-d|/detail] [--range|-r|/range]
		/// </param>
		/// <remarks>
		/// Parameters:
		/// - file: Required positional path to the QFX/OFX file to process.
		/// - --detail, -d, /detail: Optional flag to print all fields for each transaction (detailed view).
		/// - --range, -r, /range: Optional flag to print the date range (oldest and newest transaction) only.
		/// The --detail and --range options are mutually exclusive; if both are provided the program
		/// will print an error and set a non-zero exit code.
		/// Examples:
		///   QFXView transactions.qfx
		///   QFXView transactions.qfx /detail
		///   QFXView statement.ofx -range
		///   QFXView "C:\Downloads\*.qfx" 
		/// </remarks>
		static async Task<int> Main(string[] args)
		{
			// Positional argument: file path (required)
			var fileArg = new Argument<string>("file") { Description = "Path to QFX/OFX/QFX file" };

			var detailOption = 
				new Option<bool>(new[] { "--detail", "-d", "/detail", "/d" }) 
					{ Description = "Show detail of all fields" };

			var rangeOption = 
				new Option<bool>(new[] { "--range", "-r", "/range", "/r" }) 
					{ Description = "Show date range (oldest and newest transaction)" };

			var root = new RootCommand
			{
				fileArg,
				detailOption,
				rangeOption
			};

			root.SetHandler((file, showDetail, showRange) =>
			{
				if (showDetail && showRange)
				{
					Console.Error.WriteLine("Options /detail and /range are mutually exclusive.");
					Environment.ExitCode = 1;
					return;
				}


				var files = ResolveFilePaths(file).ToList();
				if (!files.Any())
				{
					Console.Error.WriteLine($"No files match: {file}");
					Environment.ExitCode = 1;
					return;
				}

				if (showRange)
				{
					foreach (var f in files)
					{
						Console.WriteLine($"File: {f}");
						PrintRange(f);
					}
					return;
				}

				foreach (var f in files)
				{
					ListTransactions(f, showDetail);
				}

				return;
			}, fileArg, detailOption, rangeOption);

			return await root.InvokeAsync(args);
		}

		static MatchCollection? GetTransactions(string filePath)
		{
			if (!File.Exists(filePath))
			{
				Console.WriteLine($"File not found: {filePath}");
				return null;
			}

			string text = File.ReadAllText(filePath);

			var stmtMatches = QfxHelpers.StmtRegex.Matches(text);

			if (stmtMatches.Count == 0)
			{
				Console.WriteLine("No transactions found.");
				return null;
			}

			return stmtMatches;
		}


		static void ListTransactions(string filePath, bool showDetail)
		{
			if (!File.Exists(filePath))
			{
				Console.WriteLine($"File not found: {filePath}");
				return;
			}

			string text = File.ReadAllText(filePath);

			// Find each <STMTTRN>...</STMTTRN> block (singleline so '.' matches newlines)
			var stmtMatches = QfxHelpers.StmtRegex.Matches(text);
			
			// Find tags like <TAG>value (OFX/QFX often uses SGML-style tags without explicit closing tags)
			// tagRegex is available via QfxHelpers.TagRegex

			if (stmtMatches.Count == 0)
			{
				Console.WriteLine("No transactions found.");
				return;
			}

			foreach (Match stmt in stmtMatches)
			{
				string block = stmt.Groups[1].Value;
				var tx = new Transaction
				{
					Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				};

				foreach (Match match in QfxHelpers.TagRegex.Matches(block))
				{
					var tag = match.Groups["tag"].Value.Trim();
					var value = match.Groups["value"].Value.Trim();
					// store last occurrence for the tag
					tx.Fields[tag] = value;
				}

				// Populate some common typed properties if available
				if (tx.Fields.TryGetValue(QfxTags.TRNTYPE, out var typeValue))
				{
					tx.Type = typeValue;
				}

				if (tx.Fields.TryGetValue(QfxTags.FITID, out var fitIdValue))
				{
					tx.FitId = fitIdValue;
				}

				if (tx.Fields.TryGetValue(QfxTags.NAME, out var nameValue))
				{
					tx.Name = nameValue;
				}

				if (tx.Fields.TryGetValue(QfxTags.MEMO, out var memoValue))
				{
					tx.Memo = memoValue;
				}

				if (tx.Fields.TryGetValue(QfxTags.TRNAMT, out var amtStr) &&
				    decimal.TryParse(amtStr, NumberStyles.Number | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amt))
				{
					tx.Amount = amt;
				}

				if (tx.Fields.TryGetValue(QfxTags.DTPOSTED, out var dtStr))
				{
					// DTPOSTED often looks like YYYYMMDD or YYYYMMDDHHMMSS... parse first 8 digits
					var m = QfxHelpers.Date8Regex.Match(dtStr);
					if (m.Success && DateTime.TryParseExact(m.Value, QfxHelpers.DateParseFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
						tx.Date = dt;
				}

				// Output a short summary
				Console.WriteLine($"Type={tx.Type ?? "(unknown)"} Date={(tx.Date.HasValue ? tx.Date.Value.ToString(QfxHelpers.DateOutputFormat) : "(unknown)")} Amount={(tx.Amount.HasValue ? tx.Amount.Value.ToString(CultureInfo.InvariantCulture) : "(unknown)" )} Name={tx.Name ?? "(none)"} FITID={tx.FitId ?? "(none)"}");

				// If requested, print all fields for debugging
				if (showDetail)
				{
					foreach (var kv in tx.Fields)
					{
						Console.WriteLine($"  {kv.Key}: {kv.Value}");
					}
			
					Console.WriteLine();
				}
			}
		}

		static void PrintRange(string filePath)
		{
			var stmtMatches = GetTransactions(filePath);
			if (stmtMatches == null)
				return;

			DateTime? oldest = null;
			DateTime? newest = null;

			foreach (Match stmt in stmtMatches)
			{
				string block = stmt.Groups[1].Value;

				foreach (Match t in QfxHelpers.TagRegex.Matches(block))
				{
					var tag = t.Groups["tag"].Value.Trim();
					var value = t.Groups["value"].Value.Trim();

					if (string.Equals(tag, QfxTags.DTPOSTED, StringComparison.OrdinalIgnoreCase))
					{
						// DTPOSTED often looks like YYYYMMDD or YYYYMMDDHHMMSS... parse first 8 digits
						var m = QfxHelpers.Date8Regex.Match(value);

						if (m.Success && DateTime.TryParseExact(m.Value, QfxHelpers.DateParseFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
						{
							if (!oldest.HasValue || dt < oldest.Value) oldest = dt;
							if (!newest.HasValue || dt > newest.Value) newest = dt;
						}
					}
				}
			}

			if (!oldest.HasValue)
			{
				Console.WriteLine("No DTPOSTED dates found.");
				return;
			}

			Console.WriteLine($"Oldest: {oldest.Value.ToString(QfxHelpers.DateOutputFormat)}");
			Console.WriteLine($"Newest: {newest.Value.ToString(QfxHelpers.DateOutputFormat)}");
		}

		static IEnumerable<string> ResolveFilePaths(string filePath)
		{
			// If the path contains wildcard characters, expand using Directory.GetFiles
			if (filePath.IndexOfAny(['*', '?']) >= 0)
			{
				var dir = Path.GetDirectoryName(filePath);
				
				if (string.IsNullOrEmpty(dir))
				{
					dir = Directory.GetCurrentDirectory();
				}

				var pattern = Path.GetFileName(filePath);
				try
				{
					return Directory.GetFiles(dir, pattern);
				}
				catch
				{
					return Array.Empty<string>();
				}
			}

			// No wildcard, return the single path as-is
			return new[] { filePath };
		}

		internal class Transaction
		{
			// store every found field (tag -> value)
			public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			// common typed properties for convenience
			public string? Type { get; set; }
			public DateTime? Date { get; set; }
			public decimal? Amount { get; set; }
			public string? FitId { get; set; }
			public string? Name { get; set; }
			public string? Memo { get; set; }
		}
	}
}
