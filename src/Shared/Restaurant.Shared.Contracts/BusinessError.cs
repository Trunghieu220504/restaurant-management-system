namespace Restaurant.Shared.Contracts;

public sealed record BusinessError
{
    public string Code { get; }
    public string Message { get; }

    public BusinessError(string code, string message)
    {
        ContractNames.Validate(code, nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
    }
}

internal static class ContractNames
{
    public static void Validate(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var segments = value.Split('.');
        if (value.Length > 128 || segments.Length < 2 || segments.Any(segment =>
                segment.Length == 0 || segment[0] is < 'a' or > 'z' ||
                segment.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))))
            throw new ArgumentException("Use lowercase dotted identifiers.", parameterName);
    }
}
