namespace OxyKit.Render.Core;

/// <summary>Shared checks for text payload fields on render requests.</summary>
internal static class ContentField
{
    public static string Require(string? content, string fieldName = "content")
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException($"Field '{fieldName}' is required.");
        }

        return content;
    }

    public static void EnsureMaxLength(string content, int maxChars, string fieldName = "content")
    {
        if (content.Length > maxChars)
        {
            throw new ArgumentException(
                $"Field '{fieldName}' exceeds the maximum length of {maxChars:N0} characters."
            );
        }
    }
}
