using Serilog;

namespace IlsSilentPrint;

public static class PrintServer
{
    private const int MaxHtmlSizeBytes = 512_000; // 500 KB
    private const int MinCopies = 1;
    private const int MaxCopies = 10;

    public static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/health", HandleHealth);
        app.MapPost("/print", HandlePrint);
        app.MapGet("/config", HandleConfig);
    }

    private static IResult HandleHealth(IPrintConfigurationManager configManager)
    {
        var config = configManager.Load();
        var printerName = string.IsNullOrWhiteSpace(config.PrinterName)
            ? new System.Drawing.Printing.PrinterSettings().PrinterName
            : config.PrinterName;

        return Results.Ok(new { status = "ok", printer = printerName });
    }

    private static async Task<IResult> HandlePrint(
        PrintRequest? request,
        IPrintService printService,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            Log.Warning("Druckauftrag ohne Body empfangen");
            return Results.BadRequest(PrintResponse.Error("Request-Body ist erforderlich"));
        }

        if (string.IsNullOrWhiteSpace(request.Html))
        {
            Log.Warning("Druckauftrag ohne HTML empfangen");
            return Results.BadRequest(PrintResponse.Error("HTML content is required"));
        }

        if (System.Text.Encoding.UTF8.GetByteCount(request.Html) > MaxHtmlSizeBytes)
        {
            Log.Warning("HTML-Content überschreitet 500 KB");
            return Results.BadRequest(PrintResponse.Error("HTML content exceeds maximum size of 500 KB"));
        }

        if (request.Copies < MinCopies || request.Copies > MaxCopies)
        {
            Log.Warning("Ungültige Kopienanzahl: {Copies}", request.Copies);
            return Results.BadRequest(PrintResponse.Error("Copies must be between 1 and 10"));
        }

        if (!printService.IsInitialized)
        {
            Log.Error("PrintService ist nicht initialisiert");
            return Results.StatusCode(500);
        }

        try
        {
            var response = await printService.PrintHtmlAsync(request.Html, request.Copies, cancellationToken);

            return response.Success
                ? Results.Ok(response)
                : Results.StatusCode(500);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unerwarteter Fehler beim Druckauftrag");
            return Results.StatusCode(500);
        }
    }

    private static IResult HandleConfig(IPrintConfigurationManager configManager)
    {
        var config = configManager.Load();
        return Results.Ok(config);
    }
}
