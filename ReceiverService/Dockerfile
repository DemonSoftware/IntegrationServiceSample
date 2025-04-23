FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ReceiverService/ReceiverService.csproj", "ReceiverService/"]
RUN dotnet restore "ReceiverService/ReceiverService.csproj"
COPY . .
WORKDIR "/src/ReceiverService"
RUN dotnet build "ReceiverService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ReceiverService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ReceiverService.dll"]
