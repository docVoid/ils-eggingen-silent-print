namespace IlsEggingenSilentPrint;

public sealed class PrintConfiguration
{
    public string PrinterName { get; set; } = string.Empty;
    public int Port { get; set; } = 9150;
    public bool AutoStart { get; set; } = true;
    public double MarginTop { get; set; } = 3;
    public double MarginBottom { get; set; } = 3;
    public double MarginLeft { get; set; } = 3;
    public double MarginRight { get; set; } = 3;
}
