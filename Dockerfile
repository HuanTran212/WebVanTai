FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["WebVanTai/WebVanTai.csproj", "WebVanTai/"]
RUN dotnet restore "WebVanTai/WebVanTai.csproj"

# Copy remaining source code and publish
COPY . .
WORKDIR "/src/WebVanTai"
RUN dotnet publish "WebVanTai.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WebVanTai.dll"]
