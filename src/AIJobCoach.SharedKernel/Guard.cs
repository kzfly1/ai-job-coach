namespace AIJobCoach.SharedKernel;

public static class Guard
{
    public static T AgainstNull<T>(T? value, string parameterName)
    {
        return value ?? throw new ArgumentNullException(parameterName);
    }

    public static string AgainstNullOrEmpty(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be null or empty.", parameterName);
        }

        return value;
    }
}