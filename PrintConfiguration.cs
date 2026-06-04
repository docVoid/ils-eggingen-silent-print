namespace IlsSilentPrint;

public sealed class PrintConfiguration
{
    public string PrinterName { get; set; } = string.Empty;
    public int Port { get; set; } = 9150;
    public bool AutoStart { get; set; } = true;
    public double MarginTop { get; set; } = 10;
    public double MarginBottom { get; set; } = 10;
    public double MarginLeft { get; set; } = 10;
    public double MarginRight { get; set; } = 10;
}
