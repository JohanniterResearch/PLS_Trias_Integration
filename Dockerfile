# Stage 1: Build React frontend
FROM node:24.14.0-alpine AS frontend-build
WORKDIR /app/client-app
COPY client-app/package*.json ./
RUN npm ci
COPY client-app/ .
ARG VITE_MODE=production
ARG VITE_ENABLE_DEV_BUTTONS=false
RUN npx tsc -b && VITE_ENABLE_DEV_BUTTONS=${VITE_ENABLE_DEV_BUTTONS} npx vite build --mode ${VITE_MODE}

# Stage 2: Build .NET backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /app
COPY PLS.sln .
COPY src/PLS.Api/ ./src/PLS.Api/
RUN dotnet publish src/PLS.Api/PLS.Api.csproj -c Release -o /publish

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get -o Acquire::Check-Valid-Until=false -o Acquire::Check-Date=false update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=backend-build /publish .
COPY --from=frontend-build /app/client-app/dist ./wwwroot
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "PLS.Api.dll"]
