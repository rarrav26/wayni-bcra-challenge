using WayniBcra.Application;
using WayniBcra.Infrastructure;
using WayniBcra.Infrastructure.Logging;
using WayniBcra.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Centralize logs in CloudWatch (in addition to the console) under the "worker" stream.
builder.Logging.AddCloudWatchLogging(builder.Configuration, "worker");

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.Configure<NotificationDispatcherOptions>(
    builder.Configuration.GetSection(NotificationDispatcherOptions.SectionName));

// Two separate background services sharing the same host/process:
// one consumes SQS and processes imports, the other dispatches outbox notifications.
builder.Services.AddHostedService<ImportMessageConsumer>();
builder.Services.AddHostedService<NotificationDispatcher>();

var host = builder.Build();
host.Run();
