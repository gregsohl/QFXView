using System.Text.RegularExpressions;

namespace QFXView;

internal static class QfxHelpers
{
	public static readonly Regex StmtRegex = new Regex($"<{QfxTags.STMTTRN}>(.*?)</{QfxTags.STMTTRN}>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
	public static readonly Regex TagRegex = new Regex(@"<(?<tag>[A-Z0-9]+)>(?<value>[^\r\n<]*)", RegexOptions.IgnoreCase);
	public static readonly Regex Date8Regex = new Regex(@"\d{8}");
	public const string DateParseFormat = "yyyyMMdd";
	public const string DateOutputFormat = "yyyy-MM-dd";
}