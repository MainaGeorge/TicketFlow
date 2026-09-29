FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY TicketFlow.Domain/TicketFlow.Domain.csproj TicketFlow.Domain/
COPY TicketFlow.Contracts/TicketFlow.Contracts.csproj TicketFlow.Contracts/
COPY TicketFlow.Application/TicketFlow.Application.csproj TicketFlow.Application/
COPY TicketFlow.Infrastructure/TicketFlow.Infrastructure.csproj TicketFlow.Infrastructure/
COPY TicketFlow.Presentation/TicketFlow.Presentation.csproj TicketFlow.Presentation/

RUN dotnet restore TicketFlow.Presentation/TicketFlow.Presentation.csproj

COPY . .

RUN dotnet publish TicketFlow.Presentation/TicketFlow.Presentation.csproj -c Release -o /app/publish --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TicketFlow.Presentation.dll"]
