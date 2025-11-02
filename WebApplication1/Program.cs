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
using WebApplication1.Controllers;
using WebApplication1.Models;
using WebApplication1.Service;
using WidgetsDemo.Services;

// ---- Načtení .env souboru ----
DotNetEnv.Env.Load();

// ---- Build builder ----
var builder = WebApplication.CreateBuilder(args);

// ---- Logging (Serilog) ----
var logPath = Environment.GetEnvironmentVariable("APP_LOG_PATH")
              ?? "/app/logs/log.txt"; // fallback

var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14
    )
    .CreateLogger();

builder.Host.UseSerilog(logger);


// ---- Služby ----
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SwopCacheService>();
builder.Services.AddSingleton<SystemMetricsService>();
builder.Services.AddHttpClient(); // základní HttpClient
builder.Services.AddSingleton<CouchDbService>();
builder.Services.AddSingleton<CountryInfoService>();
builder.Services.AddHttpClient<ForecastWeatherController>();
builder.Services.AddHttpClient<WeatherService>();
    

builder.Services.AddSingleton<ISwopClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<SwopClient>>();
    return new SwopClient(config, factory, logger);
});

// ---- MVC + Razor lokalizace ----
builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// ---- Podporované kultury ----
var supportedCultures = new[]
{
    new CultureInfo("cs"),
    new CultureInfo("en"),
};

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

// ---- JWT konfigurace ----
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

builder.Services.AddSingleton(jwtOptions);

// ---- Swagger (OpenAPI) ----
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
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

// ---- CORS ----
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ---- Authentication / JWT ----
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
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
    };

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

// ---- Authorization ----
builder.Services.AddAuthorization();

// ---- Build app ----
var app = builder.Build();

// ---- CouchDB inicializace ----
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

// ---- Middleware / pipeline ----
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

// CORS MUSÍ být před auth
app.UseCors("DefaultCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

// ---- Endpoints ----
app.MapControllers();

// Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ---- Ruční přepnutí jazyka (endpoint) ----
// Program.cs

app.MapPost("/set-language", (HttpContext http) =>
{
    var culture = http.Request.Form["culture"].ToString();
    var returnUrl = http.Request.Form["returnUrl"].ToString();

    if (!string.IsNullOrEmpty(culture))
    {
        http.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                Secure = true 
            }
        );
    }

    // Bezpečný návrat: když returnUrl chybí nebo není relativní, pošli na "/"
    if (string.IsNullOrEmpty(returnUrl) || returnUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        return Results.Redirect("/");

    return Results.LocalRedirect(returnUrl);
});


app.Run();
