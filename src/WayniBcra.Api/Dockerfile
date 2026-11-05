# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy everything and restore+publish the API project.
COPY . .
RUN dotnet restore src/WayniBcra.Api/WayniBcra.Api.csproj
RUN dotnet publish src/WayniBcra.Api/WayniBcra.Api.csproj -c Release -o /app --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Kestrel listens on 8080 inside the container (HTTP).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WayniBcra.Api.dll"]
