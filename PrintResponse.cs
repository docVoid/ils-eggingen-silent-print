namespace IlsSilentPrint;

public sealed class PrintResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static PrintResponse Ok(string message) => new() { Success = true, Message = message };
    public static PrintResponse Error(string message) => new() { Success = false, Message = message };
}
