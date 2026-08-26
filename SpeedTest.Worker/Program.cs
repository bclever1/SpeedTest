using SpeedTest.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddHostedService<SpeedTestWorker>();
var host = builder.Build();
host.Run();
