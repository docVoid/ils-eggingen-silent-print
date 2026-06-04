namespace IlsEggingenSilentPrint;

public interface IPrintConfigurationManager
{
    PrintConfiguration Load();
    void Save(PrintConfiguration configuration);
}
