namespace EPMPolicyBuilder.Models;

/// <summary>
/// Thrown when an EPM policy upload to Graph API fails.
/// Carries the actual request JSON that was sent, so the UI can display what was transmitted.
/// </summary>
public class EpmUploadException : Exception
{
    /// <summary>The JSON body that was sent in the Graph API request, if available.</summary>
    public string? RequestJson { get; }

    public EpmUploadException(string message, string? requestJson = null, Exception? inner = null)
        : base(message, inner)
    {
        RequestJson = requestJson;
    }
}
