using MagentaTV.Services;

var builder = WebApplication.CreateBuilder(args);

// Konfigurace služeb
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Konfigurace Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MagentaTV API",
        Version = "v1",
        Description = "API pro pøístup k Magenta TV službì",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "MagentaTV Support",
            Email = "support@example.com"
        }
    });
});

// Registrace HttpClient factory
builder.Services.AddHttpClient();

// Registrace Magenta služby jako Singleton (kvùli uchování stavu pøihlášení)
builder.Services.AddSingleton<Magenta>();

// Konfigurace CORS (pokud potøebujete pøístup z webových aplikací)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    // Nebo restriktivnìjší politika:
    options.AddPolicy("MagentaTVPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Konfigurace logování
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Pøidání podpory pro response caching
builder.Services.AddResponseCaching();

// Konfigurace pro produkèní prostøedí
if (builder.Environment.IsProduction())
{
    // Pøidání HTTPS redirection
    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
        options.HttpsPort = 443;
    });

    // Pøidání HSTS
    builder.Services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365);
    });
}

var app = builder.Build();

// Konfigurace middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MagentaTV API v1");
        options.RoutePrefix = string.Empty; // Swagger UI na root URL
    });
}
else
{
    // Produkèní nastavení
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Middleware pro logování požadavkù
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation($"Request: {context.Request.Method} {context.Request.Path}");
    await next();
});

// HTTPS redirection (pouze v produkci)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// CORS
app.UseCors("AllowAll"); // nebo "MagentaTVPolicy" pro restriktivnìjší pøístup

// Response caching
app.UseResponseCaching();

// Autorizace (pokud bude potøeba v budoucnu)
// app.UseAuthentication();
// app.UseAuthorization();

// Mapování controllerù
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Error handling endpoint
app.Map("/error", () => Results.Problem("An error occurred while processing your request."));

// Informaèní endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "MagentaTV API",
    version = "1.0",
    endpoints = new[]
    {
        "/swagger - API dokumentace",
        "/health - Health check",
        "/magenta/login - Pøihlášení",
        "/magenta/channels - Seznam kanálù",
        "/magenta/epg/{channelId} - EPG pro kanál",
        "/magenta/stream/{channelId} - Stream URL",
        "/magenta/catchup/{scheduleId} - Catchup stream",
        "/magenta/playlist - M3U playlist",
        "/magenta/epgxml/{channelId} - EPG v XML formátu"
    }
}));

app.Run();