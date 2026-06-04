namespace IlsSilentPrint;

public sealed class PrintRequest
{
    public string Html { get; set; } = string.Empty;
    public int Copies { get; set; } = 1;
}
