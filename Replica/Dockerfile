# Use the official Microsoft .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Set the working directory inside the container
WORKDIR /app

# Copy the project file and restore any dependencies (via `dotnet restore`)
COPY ["Replica/Replica.csproj", "Replica/"]
RUN dotnet restore "Replica/Replica.csproj"

# Copy the rest of the application code to the container
COPY . .

# Publish the application to the /out directory in the container
RUN dotnet publish "Replica/Replica.csproj" -c Release -o /out

# Use the official Microsoft ASP.NET runtime image for running the app
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base

# Set the working directory
WORKDIR /app

# Copy the published app from the build container to the runtime container
COPY --from=build /out .

# Set the entrypoint to run the application when the container starts
ENTRYPOINT ["dotnet", "Replica.dll"]
