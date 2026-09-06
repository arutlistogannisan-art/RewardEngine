using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RewardEngine.Api;
using RewardEngine.Application;
using RewardEngine.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext());
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<CashbackRuleService>();
builder.Services.AddScoped<CashbackQueryService>();
builder.Services.AddSingleton<CashbackCalculator>();
builder.Services.AddControllers(options => options.Filters.Add(new Microsoft.AspNetCore.Mvc.ProducesAttribute("application/json")))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new OpenApiInfo { Title = "RewardEngine API", Version = "v1", Description = "Transactions and asynchronous cashback rewards." }));
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres");

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/swagger"));
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<RewardEngineDbContext>().Database.MigrateAsync();
}
app.Run();
public partial class Program;
