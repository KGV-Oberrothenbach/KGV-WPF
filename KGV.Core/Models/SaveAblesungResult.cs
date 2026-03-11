namespace KGV.Core.Models;

public sealed record SaveAblesungResult(bool Ok, string Message)
{
    public static SaveAblesungResult Success(string message = "OK") => new(true, message);
    public static SaveAblesungResult Error(string message) => new(false, message);
}
