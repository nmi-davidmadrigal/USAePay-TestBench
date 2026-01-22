namespace UsaepaySupportTestbench.Models;

public enum ApiType
{
    Rest = 0,
    Soap = 1,
    PayJsFlow = 2
}

public enum EnvironmentType
{
    Sandbox = 0
}

public static class EnvironmentTypeHelper
{
    public static EnvironmentType Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EnvironmentType.Sandbox;
        }

        return Enum.TryParse<EnvironmentType>(value, ignoreCase: true, out var parsed)
            ? parsed
            : EnvironmentType.Sandbox;
    }
}
