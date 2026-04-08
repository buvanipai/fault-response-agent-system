# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for caching
COPY ["src/FaultResponseSystem.Core/FaultResponseSystem.Core.csproj", "FaultResponseSystem.Core/"]
COPY ["src/FaultResponseSystem.Web/FaultResponseSystem.Web.csproj", "FaultResponseSystem.Web/"]

# Restore dependencies
RUN dotnet restore "FaultResponseSystem.Web/FaultResponseSystem.Web.csproj"

# Copy full source
COPY src/ .

# Build and publish
WORKDIR "/src/FaultResponseSystem.Web"
RUN dotnet publish "FaultResponseSystem.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Environmental flags for Blazor Server
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "FaultResponseSystem.Web.dll"]
