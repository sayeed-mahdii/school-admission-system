using AdmissionSystem.Api.Configuration;
using AdmissionSystem.Api.Data;
using AdmissionSystem.Api.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Clock: injected everywhere so tests can swap in a FakeTimeProvider.
builder.Services.AddSingleton(TimeProvider.System);

// HOLD_DURATION_SECONDS (default 600; invalid or non-positive values fall back to 600).
// Read lazily from IConfiguration so test hosts can override it.
builder.Services.AddOptions<HoldOptions>()
    .Configure<IConfiguration>((options, configuration) =>
        options.DurationSeconds = HoldOptions.ParseDurationSeconds(configuration[HoldOptions.EnvironmentVariable]));

// Database: ConnectionStrings__DefaultConnection, falling back to DATABASE_CONNECTION_STRING.
// No EnableRetryOnFailure(): handlers open explicit transactions with row locks.
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseNpgsql(ConnectionStringResolver.Resolve(services.GetRequiredService<IConfiguration>())));

// CORS: the browser UI runs on a different origin (localhost:3000).
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .AllowAnyOrigin()
    .WithHeaders("Content-Type", "Accept", "X-Student-Id")
    .WithMethods("GET", "POST", "DELETE", "OPTIONS")));

// Errors: every error body is ProblemDetails JSON with `code` and `message`.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    var problem = context.ProblemDetails;
    var status = problem.Status ?? context.HttpContext.Response.StatusCode;
    problem.Extensions.TryAdd("code", ErrorCodes.ForStatus(status));
    problem.Extensions.TryAdd("message", problem.Detail ?? problem.Title ?? "Request failed.");
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error =>
                string.IsNullOrWhiteSpace(error.ErrorMessage) ? $"Invalid value for '{entry.Key}'." : error.ErrorMessage))
            .ToList();
        var message = details.Count > 0
            ? $"Invalid request: {string.Join(" ", details)}"
            : "Invalid request.";
        return ApiProblem.BadRequest(message);
    });

builder.Services.AddOpenApi();

var app = builder.Build();

await DatabaseMigrator.MigrateWithRetryAsync(app.Services);

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
