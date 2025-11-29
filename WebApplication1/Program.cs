using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Quartz;
using Serilog;
using System.Globalization;
using System.Text;
using WebApplication1.Controllers;
using WebApplication1.Models;
using WebApplication1.Service;
using WidgetsDemo.Services;

// ==========================================
// 1. NAČTENÍ .ENV A ENVIRONMENT
// ==========================================
// Načte .env soubor do proměnných prostředí
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Načteme proměnné prostředí i do konfigurace (aby fungovalo @inject IConfiguration v pohledech)
builder.Configuration.AddEnvironmentVariables();

// Rychlá kontrola, zda se načetly Google proměnné (pro debugging)
var gClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENTID");
var gRedirect = Environment.GetEnvironmentVariable("GOOGLE_REDIRECTURI");
Console.WriteLine($"[ENV CHECK] Google ClientID loaded: {!string.IsNullOrEmpty(gClientId)}");
Console.WriteLine($"[ENV CHECK] Google RedirectURI: {gRedirect ?? "NENALEZENO"}");

// ==========================================
// 2. LOGOVÁNÍ (SERILOG)
// ==========================================
var logPath = Environment.GetEnvironmentVariable("APP_LOG_PATH") ?? "/app/logs/log.txt";

var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14
    )
    .CreateLogger();

builder.Host.UseSerilog(logger);

// ==========================================
// 3. SLUŽBY (DI CONTAINER)
// ==========================================

// Lokalizace
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Cache
builder.Services.AddMemoryCache();

// HTTP Klienti a Services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SwopCacheService>();
builder.Services.AddSingleton<SystemMetricsService>();
builder.Services.AddSingleton<CouchDbService>();
builder.Services.AddSingleton<CountryInfoService>();
builder.Services.AddHttpClient<ForecastWeatherController>();
builder.Services.AddHttpClient<WeatherService>();

// Registrace SWOP klienta
builder.Services.AddSingleton<ISwopClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<SwopClient>>();
    return new SwopClient(config, factory, logger);
});

// MVC Controllers + Views + JSON Nastavení
builder.Services
    .AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // DŮLEŽITÉ: Zajistí, že C# vlastnost "Location" bude v JSONu "location" (camelCase)
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// Quartz (Plánované úlohy na pozadí)
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("NewsQuartzJob");
    q.AddJob<NewsQuartzJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("NewsQuartzTrigger")
        .WithSimpleSchedule(x => x
            .WithInterval(TimeSpan.FromHours(5))
            .RepeatForever()
        )
    );
});
builder.Services.AddQuartzHostedService(options => { options.WaitForJobsToComplete = true; });

// Konfigurace podporovaných jazyků
var supportedCultures = new[] { new CultureInfo("cs"), new CultureInfo("en") };
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

// MQTT Services
builder.Services.AddSingleton<MqttNewsService>();
builder.Services.AddSingleton<NewsRepository>();
builder.Services.AddHostedService<NewsBackgroundJob>();

// ==========================================
// 4. SECURITY (JWT & AUTH)
// ==========================================
var jwtOptions = new JwtOptions
{
    Key = Environment.GetEnvironmentVariable("JWT_KEY")
          ?? throw new InvalidOperationException("JWT_KEY není nastavený v .env"),
    Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "MyApp",
    Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "MyAppClient",
    ExpireMinutes = int.TryParse(Environment.GetEnvironmentVariable("JWT_EXPIREMINUTES"), out var m) ? m : 60
};

// Debug výpis JWT nastavení
Console.WriteLine($"[JWT DEBUG] KeyPrefix={jwtOptions.Key?.Substring(0, Math.Min(5, jwtOptions.Key.Length))}...");
Console.WriteLine($"[JWT DEBUG] Issuer={jwtOptions.Issuer}, Audience={jwtOptions.Audience}");

builder.Services.AddSingleton(jwtOptions);

// Povolení detailních logů pro IdentityModel (jen pro debug)
IdentityModelEventSource.ShowPII = true;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
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
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ClockSkew = TimeSpan.Zero // Token expiruje přesně v daný čas
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = ctx =>
        {
            Console.WriteLine($"[JWT ERROR] Auth failed: {ctx.Exception?.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            var email = ctx.Principal?.FindFirst("email")?.Value ?? "N/A";
            Console.WriteLine($"[JWT INFO] Token validated for user: {email}");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
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
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { securityScheme, Array.Empty<string>() } });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// ==========================================
// 5. PROXY CONFIG (DŮLEŽITÉ PRO NGINX)
// ==========================================
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost; // Důležité pro zachování domény a portu z Nginx

    // V Dockeru neznáme IP proxy předem, proto vyčistíme limity
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ==========================================
// 6. PIPELINE APLIKACE
// ==========================================
var app = builder.Build();

// Inicializace DB
using (var scope = app.Services.CreateScope())
{
    try
    {
        var couch = scope.ServiceProvider.GetRequiredService<CouchDbService>();
        await couch.EnsureDbExistsAsync();
        await couch.CreateIndexesAsync();
        Console.WriteLine("✅ CouchDB databáze připravena.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Chyba inicializace DB: {ex.Message}");
    }
}

// 1. Forwarded Headers - MUSÍ BÝT PRVNÍ
app.UseForwardedHeaders();

// Middleware pro opravu PathBase, pokud ji Nginx posílá
app.Use((context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-Forwarded-Path-Base", out var pathBase))
    {
        context.Request.PathBase = new PathString(pathBase);
    }
    return next();
});

// 2. Developer exceptions / Swagger
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.DefaultModelsExpandDepth(-1));
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // HSTS zapneme jen pokud jsme si jistí HTTPS
    // app.UseHsts(); 
}

// 3. Static Files & Routing
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 4. CORS
app.UseCors("DefaultCorsPolicy");

// 5. Lokalizace
var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

// 6. Auth
app.UseAuthentication();
app.UseAuthorization();

// 7. Endpoints
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Endpoint pro změnu jazyka
app.MapPost("/set-language", (HttpContext http) =>
{
    var culture = http.Request.Form["culture"].ToString();
    var returnUrl = http.Request.Form["returnUrl"].ToString();

    if (!string.IsNullOrEmpty(culture))
    {
        http.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, HttpOnly = false, Secure = true }
        );
    }

    if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith("/"))
        return Results.Redirect("/");

    return Results.LocalRedirect(returnUrl);
});

app.Run();