# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/WayniBcra.Worker/WayniBcra.Worker.csproj
RUN dotnet publish src/WayniBcra.Worker/WayniBcra.Worker.csproj -c Release -o /app --no-restore

# ---- runtime stage ----
# Non-web runtime: the Worker is a background service, not an HTTP server.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "WayniBcra.Worker.dll"]
