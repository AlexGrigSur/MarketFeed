FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish "MarketFeed.MockStockExchanges.ExchangeA/MarketFeed.MockStockExchanges.ExchangeA.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MarketFeed.MockStockExchanges.ExchangeA.dll"]
