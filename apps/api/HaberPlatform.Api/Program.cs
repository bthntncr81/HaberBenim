using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Middleware;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services;
using HaberPlatform.Api.Services.Media;
using HaberPlatform.Api.Services.Publishing;
using HaberPlatform.Api.Services.Reporting;
using HaberPlatform.Api.Services.XIntegration;
using HaberPlatform.Api.Services.Instagram;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Haber Benim API",
        Version = "v1",
        Description = "News aggregation platform API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Register services
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<RuleEngineService>();
builder.Services.AddScoped<EditorialService>();

// RSS Ingestion Services
builder.Services.AddHttpClient("RssFetcher", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HaberPlatform/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/rss+xml, application/xml, text/xml");
});
builder.Services.AddSingleton<RssIngestionService>();
builder.Services.AddHostedService<RssIngestionBackgroundService>();

// Publishing Configuration
builder.Services.Configure<PublishingOptions>(
    builder.Configuration.GetSection(PublishingOptions.SectionName));

// Publishing Services
builder.Services.AddScoped<IChannelPublisher, WebPublisher>();
builder.Services.AddScoped<IChannelPublisher, MobilePublisher>();
builder.Services.AddScoped<IChannelPublisher, XPublisher>();
builder.Services.AddScoped<PublisherOrchestrator>();
builder.Services.AddScoped<PublishJobService>();
builder.Services.AddSingleton<PublishJobWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PublishJobWorker>());

// Reporting Configuration (Sprint 7)
builder.Services.Configure<ReportsOptions>(
    builder.Configuration.GetSection(ReportsOptions.SectionName));

// Reporting Services
builder.Services.AddScoped<DailyReportService>();
builder.Services.AddHostedService<DailyReportWorker>();

// Alert and Breaking News Services (Sprint 8)
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<BreakingNewsService>();

// Media Pipeline Services (Sprint 10)
builder.Services.Configure<MediaOptions>(
    builder.Configuration.GetSection("Media"));
builder.Services.Configure<AIImageOptions>(
    builder.Configuration.GetSection("AIImage"));

builder.Services.AddHttpClient("MediaDiscovery", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HaberPlatform/1.0 (Media Discovery Bot)");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("MediaDownload", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HaberPlatform/1.0 (Media Fetch)");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient("Pollinations", client =>
{
    client.Timeout = TimeSpan.FromSeconds(90); // AI generation timeout
    client.DefaultRequestHeaders.Add("User-Agent", "HaberPlatform/1.0");
});

builder.Services.AddScoped<MediaDiscoveryService>();
builder.Services.AddScoped<MediaDownloadService>();
builder.Services.AddScoped<IImageGenerator, PollinationsImageGenerator>();
builder.Services.AddScoped<MediaPipelineService>();

// X Integration Services (Sprint 9)
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")))
    .SetApplicationName("HaberPlatform");

builder.Services.Configure<XIngestionOptions>(
    builder.Configuration.GetSection(XIngestionOptions.SectionName));

builder.Services.AddHttpClient("XApi", client =>
{
    // X API base URL (official docs): api.x.com
    // Override via config X:ApiBaseUrl if needed.
    client.BaseAddress = new Uri(builder.Configuration["X:ApiBaseUrl"] ?? "https://api.x.com");
    client.DefaultRequestHeaders.Add("User-Agent", "HaberPlatform/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<XOAuthService>();
builder.Services.AddScoped<XApiClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("XApi");
    var logger = sp.GetRequiredService<ILogger<XApiClient>>();
    return new XApiClient(httpClient, logger);
});
builder.Services.AddScoped<XIngestionService>();
builder.Services.AddHostedService<XIngestionWorker>();

// Instagram Integration Services (Sprint 11)
builder.Services.Configure<MetaGraphOptions>(options =>
{
    options.GraphVersion = builder.Configuration["Meta:GraphVersion"] ?? "v19.0";
    options.BaseUrl = builder.Configuration["Meta:BaseUrl"] ?? "https://graph.facebook.com";
});
builder.Services.Configure<InstagramPublishingOptions>(options =>
{
    options.Enabled = builder.Configuration.GetValue<bool>("Publishing:Instagram:Enabled", false);
    options.MaxCaptionLength = builder.Configuration.GetValue<int>("Publishing:Instagram:MaxCaptionLength", 2200);
    options.DefaultHashtags = builder.Configuration["Publishing:Instagram:DefaultHashtags"];
});

builder.Services.AddHttpClient<InstagramApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Meta:BaseUrl"] ?? "https://graph.facebook.com");
    client.DefaultRequestHeaders.Add("User-Agent", "HaberPlatform/1.0");
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddScoped<InstagramOAuthService>();
builder.Services.AddScoped<IChannelPublisher, InstagramPublisher>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminCors", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:4201")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Apply migrations and seed data in Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("Applying database migrations...");
    await db.Database.MigrateAsync();
    
    logger.LogInformation("Seeding database...");
    await DbSeeder.SeedAsync(db, logger);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Haber Benim API v1");
    });
}

app.UseCors("AdminCors");

// Serve static media files from /media path
var mediaOptions = builder.Configuration.GetSection("Media").Get<MediaOptions>() ?? new MediaOptions();
var mediaRootDir = mediaOptions.RootDir;
if (!Path.IsPathRooted(mediaRootDir))
{
    mediaRootDir = Path.Combine(app.Environment.ContentRootPath, mediaRootDir);
}

// Ensure media directory exists
Directory.CreateDirectory(mediaRootDir);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRootDir),
    RequestPath = mediaOptions.PublicBasePath,
    ContentTypeProvider = new FileExtensionContentTypeProvider(new Dictionary<string, string>
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif"
    }),
    OnPrepareResponse = ctx =>
    {
        // Cache static media files for 1 day
        ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=86400";
    }
});

// Add audit logging middleware
app.UseAuditLogging();

app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Health endpoint
app.MapGet("/health", () => new { status = "ok", service = "api" })
    .WithName("Health")
    .WithOpenApi();

// Version endpoint
app.MapGet("/api/v1/version", (IWebHostEnvironment env) =>
{
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    return new
    {
        version,
        environment = env.EnvironmentName
    };
})
.WithName("Version")
.WithOpenApi();

app.Run();
