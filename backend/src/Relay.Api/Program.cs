using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Relay.Api.Options;
using Relay.Application.Baseline;
using Relay.Infrastructure;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// The contract the Angular feature is built against is inspectable at /swagger while it is written
// (task 5.8). The XML comments on the controller and the DTOs are what make it readable.
builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Relay.Api.xml"));
});

// Turns an unhandled exception into a ProblemDetails body rather than a stack trace, and gives the
// bare status codes (the controller's 404, a routing miss) a body of the same shape.
builder.Services.AddProblemDetails();

builder.Services.AddOptions<FrontendOptions>()
    .Bind(builder.Configuration.GetSection(FrontendOptions.SectionName))
    .ValidateDataAnnotations()
    // Fail at startup with a clear message rather than on the browser's first preflight.
    .ValidateOnStart();

// The CORS policy reads the validated options rather than configuration a second time, so the policy
// and the fail-fast validation above cannot disagree about the origin list.
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IOptions<FrontendOptions>>((cors, frontend) =>
        cors.AddPolicy(FrontendCorsPolicy, policy => policy
            .WithOrigins(frontend.Value.AllowedOrigins.ToArray())
            .WithMethods(HttpMethods.Get)
            .AllowAnyHeader()));

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

// Not guarded by IsDevelopment: the SPA is built against this contract, and there is nothing behind
// this API to protect — no auth, no tenancy, one throwaway dataset.
app.UseSwagger();
app.UseSwaggerUI();

// In Development the framework installs its own developer exception page ahead of any middleware
// registered here, so this shaped 500 is what every other environment returns.
app.UseExceptionHandler();
app.UseStatusCodePages();

// No UseHttpsRedirection: the API is served over plain HTTP behind docker compose, and redirecting
// would break the documented local URLs. No UseAuthorization either — a single trusted admin
// identity, no auth layer (PRD § Out of Scope).
app.UseCors(FrontendCorsPolicy);

app.MapControllers();

app.Run();
