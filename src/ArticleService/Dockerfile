# Stage 1: Build applikation
# Der bruges .NET 7 sdk
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Kopier alle .csproj filer
COPY ArticleService/ArticleService.csproj ArticleService/
COPY CommentService/CommentService.csproj CommentService/
COPY Shared/Shared.csproj Shared/

# Restore dependencies i ArticleService projektet
RUN dotnet restore "ArticleService/ArticleService.csproj"

# kopier resten af applikationens source kode
COPY . .

# Publish ArticleService projektet
WORKDIR "/src/ArticleService"
RUN dotnet publish "ArticleService.csproj" -c Release -o /app/publish

# Opret runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ArticleService.dll"]
