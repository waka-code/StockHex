# ---------------------------------------------------------------- build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Se copia primero el csproj para que la capa de restore quede en caché
# y no se repita en cada cambio de código.
COPY ["StockHex API.csproj", "./"]
RUN dotnet restore "StockHex API.csproj"

COPY . .
RUN dotnet publish "StockHex API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ---------------------------------------------------------------- runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# wget para el healthcheck del compose.
RUN apt-get update \
 && apt-get install -y --no-install-recommends wget \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Usuario sin privilegios, provisto por la imagen base.
USER $APP_UID

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "StockHex API.dll"]
