// Moto.Marketplace.Api/Program.cs
// API REST complète pour le marketplace (thèmes, langues, snippets, plugins).
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moto.Marketplace.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddSingleton<ThemeCatalogService>();
builder.Services.AddSingleton<LanguageCatalogService>();
builder.Services.AddSingleton<SnippetCatalogService>();
builder.Services.AddSingleton<SignatureVerificationService>();

var app = builder.Build();

// ── Thèmes ──
app.MapGet("/api/v1/themes", async (ThemeCatalogService service, string? search, string? tag) =>
    Results.Ok(await service.GetCatalogAsync(search, tag)));

app.MapGet("/api/v1/themes/{id}", async (ThemeCatalogService service, string id) =>
{
    var theme = await service.GetByIdAsync(id);
    return theme != null ? Results.Ok(theme) : Results.NotFound();
});

app.MapPost("/api/v1/themes/submit", async (ThemeCatalogService service, SignatureVerificationService signer, ThemeSubmission submission) =>
{
    if (!signer.Verify(submission.ThemeJson, submission.Signature))
        return Results.BadRequest(new { error = "Signature invalide" });

    var result = await service.SubmitAsync(submission);
    return result ? Results.Ok() : Results.BadRequest();
});

// ── Langues ──
app.MapGet("/api/v1/languages", async (LanguageCatalogService service) =>
    Results.Ok(await service.GetCatalogAsync()));

app.MapPost("/api/v1/languages/submit", async (LanguageCatalogService service, SignatureVerificationService signer, LanguageSubmission submission) =>
{
    if (!signer.Verify(submission.PackJson, submission.Signature))
        return Results.BadRequest(new { error = "Signature invalide" });

    var result = await service.SubmitAsync(submission);
    return result ? Results.Ok() : Results.BadRequest();
});

// ── Snippets ──
app.MapGet("/api/v1/snippets", async (SnippetCatalogService service, string? language, string? search) =>
    Results.Ok(await service.GetCatalogAsync(language, search)));

app.MapPost("/api/v1/snippets/rate", async (SnippetCatalogService service, RatingRequest request) =>
{
    var result = await service.RateAsync(request);
    return result ? Results.Ok() : Results.BadRequest();
});

// ── Santé ──
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
}));

app.Run();

// Ajouter dans Moto.Marketplace.Api/Program.cs
var app = builder.Build();

// Activer WebSocket
app.UseWebSockets();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws/analytics")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var clientId = Guid.NewGuid().ToString();
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var wsServer = context.RequestServices.GetRequiredService<AnalyticsWebSocketServer>();
            await wsServer.HandleClientAsync(webSocket, clientId);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    }
    else
    {
        await next();
    }
});

// ── Modèles de requêtes ──
public record ThemeSubmission(string ThemeJson, string Signature, string AuthorEmail);
public record LanguageSubmission(string PackJson, string Signature, string AuthorEmail);
public record RatingRequest(string SnippetId, int Rating, string UserId);
