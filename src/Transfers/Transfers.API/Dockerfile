FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build
WORKDIR /app
EXPOSE 80
EXPOSE 443


RUN dotnet tool install -g dotnet-references --version 0.0.7
ENV PATH="${PATH}:/root/.dotnet/tools"

COPY *.sln ./
COPY src/*/*/*.csproj src/
RUN dotnet references fix -ep ./IstioTrial.sln -wd . -rupf
# Instead of dot references we can copy the proj file into the correct folder by move command

RUN dotnet restore "IstioTrial.sln"

COPY . .
WORKDIR /app/src/Transfers/Transfers.API
RUN dotnet publish -c Release -o out

FROM build AS publish

FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine-amd64 AS runtime
RUN apk add curl
WORKDIR /app
COPY --from=publish /app/src/Transfers/Transfers.API/out ./

ENTRYPOINT ["dotnet", "Transfers.API.dll"]
