FROM microsoft/dotnet:sdk as build-env
WORKDIR /phorkus
COPY src/ ./
WORKDIR /phorkus/Phorkus.Console
RUN dotnet restore
RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:aspnetcore-runtime
RUN apt update && apt install -y libc6-dev libsnappy-dev
WORKDIR /phorkus
COPY --from=build-env /phorkus/Phorkus.Console/out .
ARG CONFIG=src/Phorkus.Console/config.json
COPY ${CONFIG} ./config.json
ENTRYPOINT ["dotnet", "Phorkus.Console.dll"]
