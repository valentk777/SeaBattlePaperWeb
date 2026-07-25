using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SeaBattlePaper.Api.Configurations;
using SeaBattlePaper.Api.Hubs;
using SeaBattlePaper.Application;
using SeaBattlePaper.Infrastructure;
using SeaBattlePaper.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddSingleton<SeaBattleConnectionRegistry>();
builder.Services.AddHealthChecks();
builder.AddApiObservability();

var app = builder.Build();
app.UsePathBase("/sea-battle-paper");

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SeaBattleDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<SeaBattleHub>("/ship-hubs/sea-battle");
app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");

await app.RunAsync();
