using System.Text.RegularExpressions;
using System.Globalization;

namespace QFXView
{
	internal class Program
	{
		static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				Console.WriteLine("Usage: QFXView <file> [/all]");
				return;
			}

			bool showAll = false;
			string? filePath = null;

			foreach (var a in args)
			{
				if (a.Equals("/all", StringComparison.OrdinalIgnoreCase) ||
					a.Equals("--all", StringComparison.OrdinalIgnoreCase) ||
					a.Equals("-a", StringComparison.OrdinalIgnoreCase))
				{
					showAll = true;
				}
				else if (filePath == null)
				{
					filePath = a;
				}
			}

			if (string.IsNullOrWhiteSpace(filePath))
			{
				Console.WriteLine("Usage: QFXView <file> [/all]");
				return;
			}

			Parse(filePath, showAll);
		}

		static void Parse(string filePath, bool showAll)
		{
			if (!File.Exists(filePath))
			{
				Console.WriteLine($"File not found: {filePath}");
				return;
			}

			string text = File.ReadAllText(filePath);

			// Find each <STMTTRN>...</STMTTRN> block (singleline so '.' matches newlines)
			var stmtRegex = new Regex(@"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
			// Find tags like <TAG>value (OFX/QFX often uses SGML-style tags without explicit closing tags)
			var tagRegex = new Regex(@"<(?<tag>[A-Z0-9]+)>(?<value>[^\r\n<]*)", RegexOptions.IgnoreCase);

			var stmtMatches = stmtRegex.Matches(text);
			if (stmtMatches.Count == 0)
			{
				Console.WriteLine("No transactions found.");
				return;
			}

			int idx = 1;
			foreach (Match stmt in stmtMatches)
			{
				string block = stmt.Groups[1].Value;
				var tx = new Transaction
				{
					Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				};

				foreach (Match t in tagRegex.Matches(block))
				{
					var tag = t.Groups["tag"].Value.Trim();
					var value = t.Groups["value"].Value.Trim();
					// store last occurrence for the tag
					tx.Fields[tag] = value;
				}

				// Populate some common typed properties if available
				if (tx.Fields.TryGetValue("TRNTYPE", out var typeValue))
					tx.Type = typeValue;
				if (tx.Fields.TryGetValue("FITID", out var fitIdValue))
					tx.FitId = fitIdValue;
				if (tx.Fields.TryGetValue("NAME", out var nameValue))
					tx.Name = nameValue;
				if (tx.Fields.TryGetValue("MEMO", out var memoValue))
					tx.Memo = memoValue;

				if (tx.Fields.TryGetValue("TRNAMT", out var amtStr) &&
					decimal.TryParse(amtStr, NumberStyles.Number | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amt))
				{
					tx.Amount = amt;
				}

				if (tx.Fields.TryGetValue("DTPOSTED", out var dtStr))
				{
					// DTPOSTED often looks like YYYYMMDD or YYYYMMDDHHMMSS... parse first 8 digits
					var m = Regex.Match(dtStr, @"\d{8}");
					if (m.Success && DateTime.TryParseExact(m.Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
						tx.Date = dt;
				}

				// Output a short summary
				Console.WriteLine($"Type={tx.Type ?? "(unknown)"} Date={(tx.Date.HasValue ? tx.Date.Value.ToString("yyyy-MM-dd") : "(unknown)")} Amount={(tx.Amount.HasValue ? tx.Amount.Value.ToString(CultureInfo.InvariantCulture) : "(unknown)")} Name={tx.Name ?? "(none)"} FITID={tx.FitId ?? "(none)"}");

				// If requested, print all fields for debugging
				if (showAll)
				{
					foreach (var kv in tx.Fields)
					{
						Console.WriteLine($"  {kv.Key}: {kv.Value}");
					}
				}

				//Console.WriteLine();
			}
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
