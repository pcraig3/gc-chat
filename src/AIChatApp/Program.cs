using AIChatApp.Components;
using AIChatApp.Helpers;
using AIChatApp.Models;
using AIChatApp.Services;
using Microsoft.Extensions.Options;
using StartupExtensions;

var logger = LoggerFactory
    .Create(builder => builder.AddConsole())
    .CreateLogger("Startup");

var builder = WebApplication.CreateBuilder(args);

// Add support for a local configuration file, which doesn't get committed to source control
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

// Configures request localization with route-based culture detection (e.g., "/en", "/fr")
builder.Services.AddLocalizationSupport("en", "fr");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<JsonLocalizationService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure AI related features
builder.Services.AddKernel();

// Add users
builder.Services.AddScoped<UserService>();

// Add SearchServiceClient if config vars exist
// - builder.Configuration["search-endpoint"]
// - builder.Configuration["search-index-name"]
// - builder.Configuration["search-api-key"]
await builder.AddSearchServiceIfAvailableAsync(logger);

// Add BlobServiceClient if config vars exist
// - builder.Configuration["storage-connection-string"]
builder.AddBlobStorageIfAvailable(logger);

// Add CosmosDbClient if config vars exist
// - builder.Configuration["cosmosdb-connection-string"]
await builder.AddCosmosClientIfAvailableAsync(logger);

// Configure what AI model provider we are running
// For us, this will generally be Azure OpenAI
builder.ConfigureAiProvider(logger);

builder.Services.AddScoped<ConfigHelper>();
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<ConversationService>();

// Add MVC controllers for testing
builder.Services.AddControllers();

var app = builder.Build();

// Add logging to show supported cultures and current culture
var locOptions = app.Services.GetService<IOptions<RequestLocalizationOptions>>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Redirects root requests ("/") to "/fr" or "/en" based on Accept-Language header
app.UseAcceptLanguageRedirect();
// Sets localization options using the configured culture providers (e.g., route-based)
app.UseRequestLocalization();
// Sets the current culture based on the first path segment (e.g., "/en/chat")
app.UsePathBasedCulture();
// Redirects unmatched routes (404s) to a custom "/not-found" page
app.UseCustom404();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// And register the controller endpoints
app.MapControllers();


// Configure routing
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

// Configure APIs for chat related features
//app.MapPost("/chat", (ChatRequest request, ChatHandler chatHandler) => (chatHandler.); // Uncomment for a non-streaming response
app.MapPost("/chat/stream", (ChatRequest request, ChatService chatHandler) => chatHandler.Stream(request));

app.Run();