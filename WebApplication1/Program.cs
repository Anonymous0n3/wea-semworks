using DotNetEnv;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Serilog;
using System.Globalization;
using WebApplication1.Service;
using WidgetsDemo.Services;

// ---- Načtení .env souboru ----
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<WeatherService>();
// ---- Logging (Serilog) ----
var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog(logger);

// ---- Localization services ----
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// MVC + Razor lokalizace (view + DataAnnotations)
builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// ---- Tvoje služby / HTTP klienti ----
builder.Services.AddSingleton<SystemMetricsService>();
builder.Services.AddHttpClient(); // základní HttpClient

// CouchDbService jako singleton
builder.Services.AddSingleton<CouchDbService>();

// SWOP klient přes factory (řeší konstruktor s IConfiguration + IHttpClientFactory)
builder.Services.AddSingleton<ISwopClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new SwopClient(config, factory);
});

// ---- Podporované kultury ----
var supportedCultures = new[]
{
    new CultureInfo("cs"),
    new CultureInfo("en"),
};

// ---- RequestLocalizationOptions přes DI ----
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("cs");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

var app = builder.Build();

// ---- Pipeline ----
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

// ---- Endpoint pro ruční přepnutí jazyka ----
app.MapPost("/set-language", async (HttpContext http) =>
{
    var form = await http.Request.ReadFormAsync();
    var culture = form["culture"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(culture))
        culture = "cs";

    http.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            Secure = true,
            SameSite = SameSiteMode.Lax
        }
    );

    return Results.Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
});

// ---- Debug: kultura ----
app.MapGet("/debug-culture", (HttpContext http) =>
{
    var culture = CultureInfo.CurrentCulture.Name;
    var ui = CultureInfo.CurrentUICulture.Name;
    var haveCookie = http.Request.Cookies.ContainsKey(
        CookieRequestCultureProvider.DefaultCookieName
    );
    return Results.Json(new { culture, ui, haveCookie });
});

// ---- Default controller route ----
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

//// ---- Seed CouchDB a test SWOP request ----
//using (var scope = app.Services.CreateScope())
//{
//    var couch = scope.ServiceProvider.GetRequiredService<CouchDbService>();
//    await couch.EnsureDbExistsAsync();
//
//    // ---- Test: náhodná měna Euro ----
//    var swop = scope.ServiceProvider.GetRequiredService<ISwopClient>();
//    try
//    {
//        Console.WriteLine("Test SWOP request: EUR -> USD (aktuální kurz)...");
//        var rate = await swop.GetLatestRateAsync("EUR", "USD");
//        Console.WriteLine($"EUR -> USD: {rate}");
//
//        var hist = await swop.GetHistoricalRatesAsync("EUR", "USD", HistoricalInterval.Week);
//        Console.WriteLine($"Historická data (poslední týden), počet bodů: {hist.Count}");
//        foreach (var point in hist)
//        {
//            Console.WriteLine($"{point.Timestamp:yyyy-MM-dd} : {point.Rate}");
//        }
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine($"Chyba při SWOP requestu: {ex.Message}");
//    }
//}

app.Run();
