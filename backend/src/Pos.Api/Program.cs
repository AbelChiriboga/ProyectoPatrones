using Pos.Api.ErrorHandling;
using Pos.Application;
using Pos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// La API key de Odoo vive en user-secrets (Development) o en la variable Odoo__ApiKey; nunca en el repositorio.
Console.WriteLine($"Entorno: {builder.Environment.EnvironmentName} | Odoo:ApiKey configurada: {!string.IsNullOrWhiteSpace(builder.Configuration["Odoo:ApiKey"])}");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PosExceptionHandler>();

const string FrontendCors = "frontend";
builder.Services.AddCors(options => options.AddPolicy(FrontendCors, policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(FrontendCors);
app.MapControllers();

app.Run();

public partial class Program;
