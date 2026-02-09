using System.Text.Json;

namespace NordAPI.Swish.Errors;

/// <summary>
/// Represents an error returned by the Swish API.
/// </summary>
public sealed class SwishApiError
{
    /// <summary>
    /// The Swish-specific error code.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// The human-readable error message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Attempts to parse a Swish API error from a JSON response body.
    /// </summary>
    /// <param name="body">The raw response body.</param>
    /// <returns>
    /// A <see cref="SwishApiError"/> instance if parsing succeeds; otherwise <c>null</c>.
    /// </returns>
    public static SwishApiError? TryParse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            return JsonSerializer.Deserialize<SwishApiError>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns a readable string representation of the error.
    /// </summary>
    public override string ToString() => $"{Code}: {Message}";
}
