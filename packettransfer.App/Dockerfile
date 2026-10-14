# ایمیج اجرایی میزبان. از ریشهٔ ریپو build می‌شود:
#   docker build -f packettransfer.App/Dockerfile -t packettransfer-app .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY packettransfer.Domain/ ./packettransfer.Domain/
COPY packettransfer.Application/ ./packettransfer.Application/
COPY packettransfer.Infrastructure/ ./packettransfer.Infrastructure/
COPY packettransfer.App/ ./packettransfer.App/
RUN dotnet publish packettransfer.App/packettransfer.App.csproj -c Release -o /app/publish --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "packettransfer.App.dll"]
