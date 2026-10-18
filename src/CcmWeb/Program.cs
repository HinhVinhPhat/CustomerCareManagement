using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using CcmWeb;
using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------- data
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SessionContextInterceptor>();

// The connection string normally comes from ConnectionStrings:DefaultConnection (User Secrets in
// development). When it is missing - a fresh clone, another machine - the application falls back to
// the one written in ApplicationDbContext.FallbackConnectionString so that it still starts.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var usingFallbackConnection = string.IsNullOrWhiteSpace(connectionString);

if (usingFallbackConnection)
{
    connectionString = ApplicationDbContext.FallbackConnectionString;
}

builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
    options.UseSqlServer(connectionString)
           .AddInterceptors(serviceProvider.GetRequiredService<SessionContextInterceptor>()));

// ---------------------------------------------------------------------- application services
builder.Services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISettingService, SettingsService>();
builder.Services.AddHostedService<ContractExpiryService>();

// ---------------------------------------------------------------------- language and appearance
// Vietnamese is the default and English is the second language; every visible string comes from
// Resources/SharedResource(.en).resx (see the ccm-language skill). The language the user picked
// lives in the framework's own culture cookie; with no cookie yet the system default from SETTINGS
// answers, and the browser's Accept-Language is only the last resort.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Vietnamese letters are written to the response as UTF-8 instead of &#x110;&#x103;... escapes, so
// the page a developer reads in the browser source is the page a user reads. The characters that
// actually matter for HTML (&, <, >, " and ') are still escaped, so nothing changes about safety.
builder.Services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(UnicodeRanges.All));

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    string[] supported =
    [
        WorkConstants.Settings.Languages.Vietnamese,
        WorkConstants.Settings.Languages.English
    ];

    options.SetDefaultCulture(WorkConstants.DefaultLanguage);
    options.AddSupportedCultures(supported);
    options.AddSupportedUICultures(supported);

    // The order decides who wins. The database outranks the browser on purpose: the system default
    // is a decision a Manager took, not a guess about the visitor.
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
    options.RequestCultureProviders.Add(new SystemDefaultCultureProvider());
    options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
});

// ---------------------------------------------------------------------- MVC, authentication, authorization
builder.Services.AddControllersWithViews(options =>
    // Every POST/PUT/DELETE must carry an anti-forgery token (CSRF). Razor forms add it automatically.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    // The DataAnnotations messages in the view models are resource KEYS, so the English text written
    // in the attribute is never what the user reads.
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

// Signed in by default: an action is public only when it says [AllowAnonymous].
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Per-IP limit on the login POST.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(WorkConstants.LoginRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});

var app = builder.Build();

if (usingFallbackConnection)
{
    app.Logger.LogWarning(
        "ConnectionStrings:DefaultConnection is not configured; using the connection string written in " +
        "ApplicationDbContext ({ConnectionString}).", connectionString);
}

// ---------------------------------------------------------------------- request pipeline
app.UseExceptionHandler("/error");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Static files are served before routing and authorization; without this every asset request
// would be treated as a protected page and redirected to the sign-in screen.
app.UseStaticFiles();

// Reads the culture cookie, then the system default from SETTINGS, then Accept-Language, and sets
// CultureInfo for the whole request. Placed after UseStaticFiles so assets never pay for a query.
app.UseRequestLocalization();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
