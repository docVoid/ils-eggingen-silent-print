namespace IlsSilentPrint;

public interface IPrintConfigurationManager
{
    PrintConfiguration Load();
    void Save(PrintConfiguration configuration);
}
