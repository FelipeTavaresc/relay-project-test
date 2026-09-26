using Relay.Application.Baseline;
using Relay.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core, the connection string bound to DatabaseOptions. Infrastructure owns the EF types;
// this project only composes. Also binds IActivityQueries to its EF Core implementation.
builder.Services.AddRelayPersistence(builder.Configuration);

// The application layer is deliberately package-free — it has no DI or configuration reference of
// its own — so its services are registered here rather than behind an AddRelayApplication() of its
// own making.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBaselineCalculator, BaselineCalculator>();
builder.Services.AddScoped<IActivityBaselineService, ActivityBaselineService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
