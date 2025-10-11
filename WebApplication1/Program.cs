using DotNetEnv;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Serilog;
using System.Globalization;
using WebApplication1.Service;
using WidgetsDemo.Services;

//Loading from .env file
DotNetEnv.Env.Load();

var weatherService = new WeatherService();

// --- 1) Current Weather ---
Console.WriteLine("=== Aktuální počasí ===");
var current = await weatherService.GetCurrentWeatherAsync("Prague");
Console.WriteLine($"Město: {current.Location.Name}");
Console.WriteLine($"Teplota: {current.Current.TempC:F1}°C");
Console.WriteLine($"Podmínky: {current.Current.Condition.Text}");
Console.WriteLine($"Vlhkost: {current.Current.Humidity}%");
Console.WriteLine($"Vítr: {current.Current.WindKph} kph, směr {current.Current.WindDir}");
Console.WriteLine($"UV index: {current.Current.Uv}");
Console.WriteLine($"Rosný bod: {current.Current.DewPoint:F1}°C");
Console.WriteLine($"US EPA index kvality ovzduší: {current.Current.AirQuality?.UsEpaIndex}\n");

// --- 2) Forecast ---
Console.WriteLine("=== Předpověď počasí na 3 dny ===");
var forecast = await weatherService.GetForecastAsync("Prague", 3);
foreach (var day in forecast.Forecast.Forecastday)
{
    Console.WriteLine($"{day.Date}: {day.Day.Condition.Text}, max {day.Day.MaxtempC}°C, min {day.Day.MintempC}°C, srážky {day.Day.DailyChanceOfRain}%");
    Console.WriteLine($"UV index: {day.Day.Uv}, US EPA index: {day.Day.AirQuality?.UsEpaIndex}");
}
Console.WriteLine();

// --- 3) Search Locations ---
Console.WriteLine("=== Hledání lokací ===");
var locations = await weatherService.SearchLocationAsync("Prague");
foreach (var loc in locations)
{
    Console.WriteLine($"{loc.Name}, {loc.Region}, {loc.Country} (Lat: {loc.Lat}, Lon: {loc.Lon})");
}

Console.WriteLine("\nHotovo!");

var builder = WebApplication.CreateBuilder(args);

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

// ---- Tvoje služby / HTTP klienti (1x, bez duplikátů) ----
builder.Services.AddSingleton<SystemMetricsService>();
builder.Services.AddHttpClient("couchdb");
builder.Services.AddHttpClient<WebApplication1.Service.CouchDbService>();

// ---- Podporované kultury ----
// Používáš neutrální jazyky "cs" a "en" (drží se pak i názvy .resx souborů).
var supportedCultures = new[]
{
    new CultureInfo("cs"),
    new CultureInfo("en"),
};

// ---- RequestLocalizationOptions přes DI ----
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("cs"); // default = čeština
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Priorita detekce: 1) cookie (ruční přepnutí) 2) Accept-Language hlavička
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

// Aktivuj lokalizaci (načte z DI výše nastavené options)
var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// ---- Endpoint pro ruční přepnutí jazyka (uloží cookie) ----
// Volání: POST /set-language  (form fields: culture=cs|en, returnUrl=/něco)
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

// ---- Debug: rychlá kontrola, co je aktuální kultura ----
// GET /debug-culture  -> { culture: "...", ui: "...", haveCookie: true/false }
app.MapGet("/debug-culture", (HttpContext http) =>
{
    var culture = CultureInfo.CurrentCulture.Name;
    var ui = CultureInfo.CurrentUICulture.Name;
    var haveCookie = http.Request.Cookies.ContainsKey(
        CookieRequestCultureProvider.DefaultCookieName
    );
    return Results.Json(new { culture, ui, haveCookie });
});


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
