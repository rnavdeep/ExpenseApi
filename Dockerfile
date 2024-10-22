# Use the official ASP.NET Core runtime as a base image
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image for building the project
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy the project files
COPY ["Expense.API.csproj", "./"]

# Restore project dependencies
RUN dotnet restore "./Expense.API.csproj"

# Copy the rest of the app
COPY . .

# Build the project
RUN dotnet build "./Expense.API.csproj" -c Release -o /app/build

# Publish the app
RUN dotnet publish "./Expense.API.csproj" -c Release -o /app/publish

# Final stage: set up the runtime image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Set environment variable for the database password
ARG AUTH_DB_SA_PASSWORD
ENV SA_PASSWORD=${AUTH_DB_SA_PASSWORD}

# Entry point to start the application
ENTRYPOINT ["dotnet", "Expense.API.dll"]
