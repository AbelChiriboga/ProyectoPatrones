using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Pos.Api.ErrorHandling;
using Pos.Api.Security;
using Pos.Api.Versioning;
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
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
    });

// Autorización basada en políticas — las reglas están centralizadas aquí, no en los controladores.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(OperatorRoles.Admin, policy =>
        policy.RequireAuthenticatedUser().RequireRole(OperatorRoles.Admin));
    options.AddPolicy(OperatorRoles.Cashier, policy =>
        policy.RequireAuthenticatedUser().RequireRole(OperatorRoles.Admin, OperatorRoles.Cashier));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Documentación Swagger / OpenAPI con soporte para autenticación Bearer JWT y esquemas XML
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "POS Odoo Community API",
        Version = "v1",
        Description = "API REST del sistema Punto de Venta (POS) integrado con Odoo 20 Community vía Clean Architecture (.NET 10). " +
                      "Incluye autenticación JWT con roles (Admin/Cashier), políticas dinámicas, cotización fiscal de compras, " +
                      "cobro en efectivo exacto, idempotencia durable, historial paginado (keyset) y reportes consolidados.",
        Contact = new OpenApiContact
        {
            Name = "Equipo Patrones de Software",
            Url = new Uri("http://localhost:5173")
        }
    });

    // Definición de esquema de seguridad Bearer JWT (OpenAPI 3.0 Http Bearer)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Ingrese el token JWT obtenido en /api/auth/login para autorizar las peticiones.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document, null),
            new List<string>()
        }
    });

    // Incorporación de comentarios XML para documentación de endpoints y modelos
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PosExceptionHandler>();

const string FrontendCors = "frontend";
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var envCorsOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "")
    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var allowLan = builder.Configuration.GetValue<bool>("Cors:AllowLan", true)
               || builder.Configuration.GetValue<bool>("CORS_ALLOW_LAN", false)
               || builder.Environment.IsDevelopment();

var mergedOrigins = corsAllowedOrigins.Concat(envCorsOrigins)
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .Select(o => o.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

mergedOrigins.Add("http://localhost:5173");
mergedOrigins.Add("http://127.0.0.1:5173");

builder.Services.AddCors(options => options.AddPolicy(FrontendCors, policy =>
{
    if (mergedOrigins.Contains("*"))
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    }
    else
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            var trimmed = origin.TrimEnd('/');
            if (mergedOrigins.Contains(trimmed)) return true;

            // Admite peticiones desde clientes en la red de área local (RFC 1918)
            if (allowLan && Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                var host = uri.Host;
                if (host == "localhost" || host == "127.0.0.1") return true;
                if (host.StartsWith("192.168.", StringComparison.Ordinal) ||
                    host.StartsWith("10.", StringComparison.Ordinal) ||
                    System.Text.RegularExpressions.Regex.IsMatch(host, @"^172\.(1[6-9]|2[0-9]|3[0-1])\."))
                {
                    return true;
                }
            }

            return false;
        })
        .AllowAnyHeader()
        .AllowAnyMethod();
    }
}));

var app = builder.Build();

app.UseExceptionHandler();

// Middleware de versionamiento formal de la API (añade headers y valida X-Api-Version)
app.UseMiddleware<ApiVersioningMiddleware>();

// Swagger UI y especificación OpenAPI JSON
app.UseSwagger(c =>
{
    c.RouteTemplate = "openapi/{documentName}.json";
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/openapi/v1.json", "POS Odoo API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "POS Odoo API - Swagger UI";
    c.DisplayRequestDuration();
});

app.UseCors(FrontendCors);
app.UseAuthentication();
app.UseAuthorization();

// Todo exige token salvo los endpoints marcados con [AllowAnonymous] (login).
app.MapControllers().RequireAuthorization();

app.Run();

public partial class Program;
