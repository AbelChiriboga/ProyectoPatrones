using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pos.Api.ErrorHandling;
using Pos.Api.Security;
using Pos.Application.Auth;
using Pos.Application;
using Pos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// La API key de Odoo vive en user-secrets (Development) o en la variable Odoo__ApiKey; nunca en el repositorio.
Console.WriteLine($"Entorno: {builder.Environment.EnvironmentName} | Odoo:ApiKey configurada: {!string.IsNullOrWhiteSpace(builder.Configuration["Odoo:ApiKey"])}");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Autenticación del operador: JWT firmado con una clave que solo existe en user-secrets / variables de entorno.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();

// Todo exige token salvo los endpoints marcados con [AllowAnonymous] (login).
app.MapControllers().RequireAuthorization();

app.Run();

public partial class Program;
