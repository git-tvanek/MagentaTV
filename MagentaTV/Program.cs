using MagentaTV.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Magenta>();

// Modern� z�pis CORS, pro produkci doporu�uji omezen� na konkr�tn� dom�ny
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Ve v�voji povol Swagger a SwaggerUI, v produkci bezpe�n� pouze error handling
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
    // V produkci bys mohl m�t vlastn� ErrorController (viz dokumentace ASP.NET Core)
}

// CORS doporu�uji a� po ErrorHandleru/Swaggeru kv�li spr�vn�mu zpracov�n� hlavi�ek
app.UseCors();

app.MapControllers();

app.Run();
