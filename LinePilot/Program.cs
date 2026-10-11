using LinePilot.Data;
using LinePilot.Infrastructure;
using LinePilot.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Vercel (and other container hosts) pass the port to listen on through $PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllersWithViews(o => o.Filters.Add<NotFoundFilter>());
builder.Services.AddSingleton(TimeProvider.System);

// ---- Database: SQLite kept entirely in memory ----
// A shared-cache in-memory database lives as long as at least one connection is open, so we hold
// one "keep-alive" connection for the app's lifetime. Each request still gets its own connection.
const string connectionString = "Data Source=linepilot;Mode=Memory;Cache=Shared";
var keepAlive = new SqliteConnection(connectionString);
keepAlive.Open();
builder.Services.AddSingleton(keepAlive);
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));

builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<WorkOrderService>();
builder.Services.AddScoped<MachineService>();
builder.Services.AddScoped<InventoryService>();

// ---- Data protection (anti-forgery tokens & TempData cookies) ----
// Serverless instances are short-lived, so every instance must share the same key; otherwise a form
// rendered by one instance would be rejected by the next. The key XML comes from an environment
// variable (never committed); locally the default per-process key is fine.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("LinePilot");
if (Environment.GetEnvironmentVariable("DATA_PROTECTION_KEY_XML") is { Length: > 0 } keyXml)
{
    var keyDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "linepilot-keys"));
    File.WriteAllText(Path.Combine(keyDir.FullName, "key-shared.xml"), keyXml);
    dataProtection.PersistKeysToFileSystem(keyDir).DisableAutomaticKeyGeneration();
}

// Vercel terminates HTTPS in front of the app and forwards the original scheme and client IP.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    // Create the schema and demo data before the server starts accepting requests.
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SeedData.Populate(db, TimeProvider.System.GetUtcNow().UtcDateTime);
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStatusCodePagesWithReExecute("/Home/Error");

app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Exposed so integration tests can start the app with WebApplicationFactory.
public partial class Program;
