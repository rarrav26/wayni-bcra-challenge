using WayniBcra.Application;
using WayniBcra.Infrastructure;
using WayniBcra.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddHostedService<ImportMessageConsumer>();

var host = builder.Build();
host.Run();
