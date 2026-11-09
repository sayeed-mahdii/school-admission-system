FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies (optimizes Docker caching)
COPY AdmissionSystem.Api/AdmissionSystem.Api.csproj AdmissionSystem.Api/
RUN dotnet restore AdmissionSystem.Api/AdmissionSystem.Api.csproj

# Copy the rest of the source files and publish
COPY . .
RUN dotnet publish AdmissionSystem.Api/AdmissionSystem.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "AdmissionSystem.Api.dll"]