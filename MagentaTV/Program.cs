using MagentaTV.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Magenta>();

// Moderní zápis CORS, pro produkci doporuèuji omezení na konkrétní domény
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Ve vývoji povol Swagger a SwaggerUI, v produkci bezpeènì pouze error handling
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MagentaTV API v1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    app.UseExceptionHandler("/error");
    // V produkci bys mohl mít vlastní ErrorController (viz dokumentace ASP.NET Core)
}

// CORS doporuèuji až po ErrorHandleru/Swaggeru kvùli správnému zpracování hlavièek
app.UseCors();

app.MapControllers();

app.Run();
