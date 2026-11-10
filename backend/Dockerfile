FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files and restore dependencies
COPY AdmissionSystem.slnx ./
COPY AdmissionSystem.Api/AdmissionSystem.Api.csproj AdmissionSystem.Api/
COPY AdmissionSystem.Tests/AdmissionSystem.Tests.csproj AdmissionSystem.Tests/
RUN dotnet restore AdmissionSystem.slnx

# Copy the rest of the source files and publish
COPY . .
RUN dotnet publish AdmissionSystem.Api/AdmissionSystem.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AdmissionSystem.Api.dll"]
