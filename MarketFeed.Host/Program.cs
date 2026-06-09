using Autofac;
using Autofac.Extensions.DependencyInjection;
using MarketFeed.Host;
using MarketFeed.Host.Settings;
using NLog.Extensions.Logging;
using NLog.Web;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("nlog-config.json", optional: true, reloadOnChange: true);
builder.Logging.ClearProviders();
builder.Host.UseNLog();
NLog.LogManager.Setup().LoadConfigurationFromSection(builder.Configuration);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(container =>
    container.RegisterModule(new AutofacDI(builder.Configuration)));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

var metricsPort = app.Configuration.GetSection("Metrics").Get<MetricsConfiguration>()?.Port ?? 9100;
using var metricServer = new KestrelMetricServer(port: metricsPort);
metricServer.Start();

app.Run();