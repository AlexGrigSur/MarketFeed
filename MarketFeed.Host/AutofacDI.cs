using Autofac;
using MarketFeed.Abstractions;
using MarketFeed.DataAccess.PostgreSQL;
using MarketFeed.Host.Settings;
using NLog.Extensions.Logging;

namespace MarketFeed.Host;

public sealed class AutofacDI : Module
{
    private readonly IConfiguration _configuration;

    public AutofacDI(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    protected override void Load(ContainerBuilder builder)
    {
        RegisterClients(builder);
        RegisterRepository(builder);

        var processorOptions = _configuration.GetSection("StockExchangeProcessor").Get<StockExchangeProcessorConfiguration>() ?? new();
        builder.RegisterInstance(processorOptions).SingleInstance();

        builder.RegisterType<StockExchangeProcessor>()
            .As<IHostedService>()
            .SingleInstance();
    }

    private void RegisterClients(ContainerBuilder builder)
    {
        var clientConfigurations = _configuration.GetSection("Exchanges").Get<StockClientConfiguration[]>()
            ?? throw new InvalidOperationException("Stock exchange client configurations ('Exchanges') are missing or invalid.");

        var metrics = new PrometheusMetrics();
        builder.RegisterInstance(metrics).As<IClientMetrics>().As<IProcessorMetrics>().SingleInstance();

        var loggerFactory = LoggerFactory.Create(logging => logging.AddNLog());
        var clientFactory = new StockExchangeClientFactory(loggerFactory, metrics);

        foreach (var configuration in clientConfigurations)
        {
            var client = clientFactory.CreateClient(configuration);
            builder.RegisterInstance(client).As<IStockExchangeClient>();
        }

        builder.Register(c => c.Resolve<IEnumerable<IStockExchangeClient>>().ToArray())
            .As<IReadOnlyList<IStockExchangeClient>>()
            .SingleInstance();
    }

    private void RegisterRepository(ContainerBuilder builder)
    {
        var connectionString = _configuration.GetConnectionString("StockQuoteRepository")
            ?? throw new InvalidOperationException("Connection string 'StockQuoteRepository' is not configured.");

        builder.RegisterType<StockQuoteRepository>()
            .As<IStockQuoteRepository>()
            .WithParameter("connectionString", connectionString)
            .SingleInstance();
    }
}