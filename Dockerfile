FROM node:20-bookworm AS client-build
WORKDIR /app/client
COPY src/client/package.json src/client/package-lock.json ./
RUN npm ci
COPY src/client/ ./
RUN npm run build -- --outDir /app/client-dist

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /src
COPY . .
RUN dotnet publish src/SeaBattlePaper.Api/SeaBattlePaper.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

COPY --from=server-build /app/publish .
COPY --from=client-build /app/client-dist ./wwwroot

EXPOSE 8080
ENTRYPOINT ["dotnet", "SeaBattlePaper.Api.dll"]
