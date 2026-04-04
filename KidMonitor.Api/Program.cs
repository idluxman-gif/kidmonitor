using System.Text;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using KidMonitor.Api.Data;
using KidMonitor.Api.Endpoints;
using KidMonitor.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["DATABASE_URL"]
    ?? throw new InvalidOperationException("Postgres connection string is not configured.");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

// ── Auth ──────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<TokenService>();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "KidMonitor.Api",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "KidMonitor.Clients",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

// ── In-memory cache (used for rate limiting) ──────────────────────────────────
builder.Services.AddMemoryCache();

// ── Push notifications ────────────────────────────────────────────────────────
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

// Named HttpClients for APNs (production and sandbox endpoints differ).
builder.Services.AddHttpClient("apns-production", c =>
    c.BaseAddress = new Uri("https://api.push.apple.com/"));
builder.Services.AddHttpClient("apns-sandbox", c =>
    c.BaseAddress = new Uri("https://api.sandbox.push.apple.com/"));

// ── Firebase / FCM ────────────────────────────────────────────────────────────
InitializeFirebase(builder.Configuration);

var app = builder.Build();

// ── Migrate on startup ────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapDeviceEndpoints();
app.MapPushTokenEndpoints();
app.MapEventEndpoints();

app.Run();

// ── Helpers ───────────────────────────────────────────────────────────────────
static void InitializeFirebase(IConfiguration config)
{
    var credentialJson = config["Firebase:CredentialJson"];
    var credentialFile = config["Firebase:CredentialFile"];

    if (!string.IsNullOrWhiteSpace(credentialJson))
    {
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromJson(credentialJson),
        });
    }
    else if (!string.IsNullOrWhiteSpace(credentialFile))
    {
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromFile(credentialFile),
        });
    }
    // No Firebase config → FCM disabled; PushNotificationService skips gracefully.
}
