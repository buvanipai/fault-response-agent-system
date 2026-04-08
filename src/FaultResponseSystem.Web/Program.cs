using FaultResponseSystem.Data;
using FaultResponseSystem.Web.Components;
using FaultResponseSystem.Web.Services;
using Microsoft.Extensions.Configuration;
using dotenv.net;

// Load environment variables from .env file
DotEnv.Load(new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 4));

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register data provider and runner as singletons so state persists
// and all Blazor circuits share the same instance
builder.Services.AddSingleton<IDataProvider>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new JsonDataProvider(config);
});
builder.Services.AddSingleton<DagRunnerService>();

// ── Pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
