namespace IlsEggingenSilentPrint;

public interface IPrintService : IDisposable
{
    Task InitializeAsync();
    Task<PrintResponse> PrintHtmlAsync(string html, int copies, CancellationToken cancellationToken = default);
    bool IsInitialized { get; }
}
