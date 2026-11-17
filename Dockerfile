# Dockerfile para desplegar GeoCheck en la Nube 24/7 (Render, Railway, Fly.io, etc.)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copiar archivos de proyecto y restaurar dependencias
COPY ChecadorWhatsApp/*.csproj ./ChecadorWhatsApp/
RUN dotnet restore ./ChecadorWhatsApp/ChecadorWhatsApp.csproj

# Copiar el resto del código y compilar en Release
COPY ChecadorWhatsApp/ ./ChecadorWhatsApp/
WORKDIR /app/ChecadorWhatsApp
RUN dotnet publish -c Release -o /app/publish

# Imagen de producción ligera ASP.NET
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Instalar curl para healthchecks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Variables de entorno por defecto
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=5097

EXPOSE 5097

ENTRYPOINT ["dotnet", "ChecadorWhatsApp.dll"]
