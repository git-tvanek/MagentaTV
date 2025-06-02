using MagentaTV.Services;

var builder = WebApplication.CreateBuilder(args);

// Konfigurace slu�eb
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Konfigurace Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MagentaTV API",
        Version = "v1",
        Description = "API pro p��stup k Magenta TV slu�b�",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "MagentaTV Support",
            Email = "support@example.com"
        }
    });
});

// Registrace HttpClient factory
builder.Services.AddHttpClient();

// Registrace Magenta slu�by jako Singleton (kv�li uchov�n� stavu p�ihl�en�)
builder.Services.AddSingleton<Magenta>();

// Konfigurace CORS (pokud pot�ebujete p��stup z webov�ch aplikac�)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    // Nebo restriktivn�j�� politika:
    options.AddPolicy("MagentaTVPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Konfigurace logov�n�
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// P�id�n� podpory pro response caching
builder.Services.AddResponseCaching();

// Konfigurace pro produk�n� prost�ed�
if (builder.Environment.IsProduction())
{
    // P�id�n� HTTPS redirection
    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
        options.HttpsPort = 443;
    });

    // P�id�n� HSTS
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
    // Produk�n� nastaven�
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Middleware pro logov�n� po�adavk�
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
app.UseCors("AllowAll"); // nebo "MagentaTVPolicy" pro restriktivn�j�� p��stup

// Response caching
app.UseResponseCaching();

// Autorizace (pokud bude pot�eba v budoucnu)
// app.UseAuthentication();
// app.UseAuthorization();

// Mapov�n� controller�
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Error handling endpoint
app.Map("/error", () => Results.Problem("An error occurred while processing your request."));

// Informa�n� endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "MagentaTV API",
    version = "1.0",
    endpoints = new[]
    {
        "/swagger - API dokumentace",
        "/health - Health check",
        "/magenta/login - P�ihl�en�",
        "/magenta/channels - Seznam kan�l�",
        "/magenta/epg/{channelId} - EPG pro kan�l",
        "/magenta/stream/{channelId} - Stream URL",
        "/magenta/catchup/{scheduleId} - Catchup stream",
        "/magenta/playlist - M3U playlist",
        "/magenta/epgxml/{channelId} - EPG v XML form�tu"
    }
}));

app.Run();