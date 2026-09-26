FROM node:24-bookworm-slim AS web
WORKDIR /build/web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
RUN npm run lint && npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /build
COPY Directory.Build.props .editorconfig ./
COPY src/ ./src/
RUN dotnet publish src/Mya.Api -c Release -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api /publish/ ./
COPY --from=web /build/web/dist/mya-web/browser/ ./wwwroot/
ENV ASPNETCORE_ENVIRONMENT=Production
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Mya.Api.dll"]
