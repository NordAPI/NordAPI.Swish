namespace NordAPI.Swish;

/// <summary>
/// Represents a request to create a Swish refund for a previously completed payment.
/// </summary>
/// <param name="OriginalPaymentReference">
/// The unique payment reference (ID or token) of the original payment to be refunded.
/// </param>
/// <param name="Amount">The refund amount as a string (e.g., "100.00").</param>
/// <param name="Currency">The ISO currency code (e.g., "SEK").</param>
/// <param name="Message">Optional message describing the refund reason.</param>
/// <param name="CallbackUrl">The URL Swish will call when the refund status changes.</param>
public sealed record CreateRefundRequest(
    string OriginalPaymentReference,
    string Amount,
    string Currency,
    string Message,
    string CallbackUrl
);

/// <summary>
/// Represents the response returned after creating or querying a Swish refund.
/// </summary>
/// <param name="Id">Unique identifier of the refund transaction.</param>
/// <param name="Status">
/// Current status of the refund (e.g., "CREATED", "PENDING", "PAID", "DECLINED").
/// </param>
/// <param name="ErrorCode">Optional Swish error code if the refund failed.</param>
/// <param name="ErrorMessage">Optional human-readable error message.</param>
public sealed record CreateRefundResponse(
    string Id,
    string Status,
    string? ErrorCode = null,
    string? ErrorMessage = null
);
