# Dockerfile para compilar y ejecutar Pos.Api (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar definiciones de proyectos y restaurar
COPY ["src/Pos.Domain/Pos.Domain.csproj", "Pos.Domain/"]
COPY ["src/Pos.Application/Pos.Application.csproj", "Pos.Application/"]
COPY ["src/Pos.Infrastructure/Pos.Infrastructure.csproj", "Pos.Infrastructure/"]
COPY ["src/Pos.Api/Pos.Api.csproj", "Pos.Api/"]

RUN dotnet restore "Pos.Api/Pos.Api.csproj"

# Copiar código y publicar binario optimizado
COPY src/ .
WORKDIR "/src/Pos.Api"
RUN dotnet publish "Pos.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Escucha en 0.0.0.0 para aceptar peticiones desde cualquier interfaz de red (localhost y LAN)
ENV ASPNETCORE_URLS=http://0.0.0.0:5038
EXPOSE 5038

ENTRYPOINT ["dotnet", "Pos.Api.dll"]
