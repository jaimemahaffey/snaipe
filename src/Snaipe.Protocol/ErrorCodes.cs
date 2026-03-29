namespace Snaipe.Protocol;

/// <summary>
/// Well-known error codes returned in <see cref="ErrorResponse"/>.
/// </summary>
public static class ErrorCodes
{
    public const int UnknownMessage = 1001;
    public const int ElementNotFound = 1002;
    public const int PropertyNotFound = 1003;
    public const int PropertyReadOnly = 1004;
    public const int InvalidPropertyValue = 1005;
    public const int TreeTruncated = 1006;
    public const int SerializationError = 1007;
    public const int InternalError = 1008;
    public const int PayloadTooLarge = 1009;
}
