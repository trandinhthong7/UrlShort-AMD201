# Use official .NET SDK image for building the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy project file first for better caching
COPY UrlShort.csproj ./
RUN dotnet restore

# Copy the rest of the source code
COPY . ./

# Build the project (remove dotnet clean)
RUN dotnet publish -c Release -o /app/out

# Use a lightweight runtime image for the final container
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out ./

# Set environment variables for port binding
ENV ASPNETCORE_URLS="http://+:5050;http://+:5051"

# Expose ports and start the application
EXPOSE 5050 5051
ENTRYPOINT ["dotnet", "UrlShort.dll"]
