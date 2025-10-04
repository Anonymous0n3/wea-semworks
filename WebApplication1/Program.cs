using Microsoft.AspNetCore.Localization;
using Serilog;
using System.Globalization;

using WidgetsDemo.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.AddSingleton<SystemMetricsService>();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");


var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog(logger);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient("couchdb");
builder.Services.AddHttpClient<WebApplication1.Service.CouchDbService>();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("cs") };

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("cs"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
