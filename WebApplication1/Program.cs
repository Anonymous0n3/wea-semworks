using DotNetEnv;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Serilog;
using System.Globalization;
using WebApplication1.Service;
using WidgetsDemo.Services;

//Loading from .env file
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
