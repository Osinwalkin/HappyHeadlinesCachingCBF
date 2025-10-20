FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Kopier alle .csproj filer
COPY ArticleService/ArticleService.csproj ArticleService/
COPY CommentService/CommentService.csproj CommentService/
COPY Shared/Shared.csproj Shared/

# Restore dependencies i CommentService projektet
RUN dotnet restore "CommentService/CommentService.csproj"

# kopier hele src folderen
COPY . .

# publish CommentService projektet
WORKDIR "/src/CommentService"
RUN dotnet publish "CommentService.csproj" -c Release -o /app/publish

# Opret runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CommentService.dll"]