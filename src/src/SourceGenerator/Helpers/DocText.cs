namespace Purview.Results.SourceGenerator.Helpers;

/// <summary>
/// Composes and escapes text that is written into generated XML documentation.
/// </summary>
/// <remarks>
/// Type names such as <c>Result&lt;TValue, TError&gt;</c> or <c>List&lt;string&gt;</c> contain characters
/// that are invalid as XML content, so every name interpolated into a generated documentation block is
/// escaped; otherwise the consuming compilation reports CS1570 for the generated file.
/// </remarks>
static class DocText
{
	/// <summary>
	/// Escapes the characters that are invalid inside XML documentation content.
	/// </summary>
	/// <param name="value">The raw text.</param>
	/// <returns>The XML-safe text.</returns>
	public static string Escape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
