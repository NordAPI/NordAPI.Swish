namespace NordAPI.Swish.Internal;

internal interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

internal sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

internal static class Nonce
{
    public static string New() => Convert.ToHexString(Guid.NewGuid().ToByteArray());
}
