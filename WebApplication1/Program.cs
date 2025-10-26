using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebApplication1.Models;
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

// --------------------
// Localization (ponecháno z původního projektu)
// --------------------
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SwopCacheService>();


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

// --------------------
// JWT konfigurace (prefer .env, fallback na appsettings)
// --------------------
var jwtOptions = new JwtOptions
{
    Key = Environment.GetEnvironmentVariable("JWT_KEY")
          ?? throw new InvalidOperationException("JWT_KEY není nastavený v .env"),
    Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "MyApp",
    Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "MyAppClient",
    ExpireMinutes = int.TryParse(Environment.GetEnvironmentVariable("JWT_EXPIREMINUTES"), out var m) ? m : 60
};

Console.WriteLine($"[JWT DEBUG] KeyPrefix={jwtOptions.Key?.Substring(0, Math.Min(10, jwtOptions.Key.Length))}");
Console.WriteLine($"[JWT DEBUG] Issuer={jwtOptions.Issuer}, Audience={jwtOptions.Audience}");

if (string.IsNullOrWhiteSpace(jwtOptions.Key))
{
    // fail fast: token signing key must be provided
    throw new InvalidOperationException("JWT_KEY není nastavený v .env nebo v konfiguraci! Přidej ho a restartuj aplikaci.");
}

// přidej do DI
builder.Services.AddSingleton<JwtOptions>(jwtOptions);
var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

// ladicí výpis (nezobrazuj celý klíč v produkci)
Console.WriteLine($"[JWT DEBUG] Issuer={jwtOptions.Issuer}, Audience={jwtOptions.Audience}, KeyPrefix={jwtOptions.Key.Substring(0, Math.Min(8, jwtOptions.Key.Length))}...");

// --------------------
// Registrace služeb (CouchDbService jako typed HttpClient)
// --------------------
// CouchDbService ctor: CouchDbService(HttpClient client, JwtOptions jwtOptions, IConfiguration config)
builder.Services.AddHttpClient<CouchDbService>();

// případné další služby
builder.Services.AddTransient<WeatherService>();
builder.Services.AddSingleton<SystemMetricsService>();

// --------------------
// Swagger (OpenAPI) - s podporou JWT v UI
// --------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
app.UseRouting();
app.UseAuthorization();

// ---- Endpoint pro ruční přepnutí jazyka ----
app.MapPost("/set-language", async (HttpContext http) =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebApplication1 API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Zadejte 'Bearer {token}'"
    };

    c.AddSecurityDefinition("Bearer", securityScheme);

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, new string[] { } }
    });
});

// --------------------
// CORS
// --------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        // pro lokální vývoj; v produkci specifikuj konkrétní origin(y)
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// --------------------
// Authentication / JWT
// --------------------
IdentityModelEventSource.ShowPII = true; // pouze pro lokální debug, NE v produkci

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
// ---- Debug: kultura ----
app.MapGet("/debug-culture", (HttpContext http) =>
{
    var keyBytes = Encoding.UTF8.GetBytes(jwtOptions.Key);
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
    };

    // Debug eventy - vypíší, proč validace selhala nebo jaký token přišel
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var header = ctx.Request.Headers["Authorization"].FirstOrDefault();
            Console.WriteLine($"[JWT] OnMessageReceived Authorization header: {header}");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = ctx =>
        {
            Console.WriteLine($"[JWT] Authentication failed: {ctx.Exception?.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            Console.WriteLine("[JWT] Token validated. Claims:");
            foreach (var c in ctx.Principal.Claims)
                Console.WriteLine($"  - {c.Type} = {c.Value}");
            return Task.CompletedTask;
        }
    };
});

// --------------------
// Authorization
// --------------------
builder.Services.AddAuthorization();

// --------------------
// Build app
// --------------------
var app = builder.Build();

// CouchDB inicializace při startu (pokud CouchDbService používáš takto)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var couch = scope.ServiceProvider.GetRequiredService<CouchDbService>();
        await couch.EnsureDbExistsAsync();
        Console.WriteLine("✅ CouchDB databáze ověřena nebo vytvořena.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Chyba při inicializaci CouchDB: {ex.Message}");
    }
}

// --------------------
// Pipeline
// --------------------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.DefaultModelsExpandDepth(-1));
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// CORS MUSÍ být před authentication/authorization
app.UseCors("DefaultCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

// MVC / controllers
app.MapControllers();

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
