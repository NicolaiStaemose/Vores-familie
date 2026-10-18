# Build context: repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Build.props ./
COPY src/VoresFamilie.Web/ src/VoresFamilie.Web/
COPY contracts/ contracts/
RUN dotnet publish src/VoresFamilie.Web -c Release -o /out /p:SkipApiBuild=true

FROM nginx:alpine
COPY src/VoresFamilie.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /out/wwwroot /usr/share/nginx/html
EXPOSE 80

