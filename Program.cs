using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<UserStore>();

var app = builder.Build();

// Pipeline order: errors are caught first; authentication gates requests;
// request/response logging wraps authenticated requests and endpoint execution.
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TokenAuthenticationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.MapGet("/", () => Results.Ok(new { service = "UserManagementAPI", status = "running" }));
app.MapGet("/users", (UserStore store) => Results.Ok(store.GetAll()));
app.MapGet("/users/{id:guid}", (Guid id, UserStore store) =>
    store.Get(id) is { } user ? Results.Ok(user) : Results.NotFound(new { error = "User not found." }));

app.MapPost("/users", (UserInput input, UserStore store) =>
{
    var errors = Validate(input);
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    var user = store.Create(input);
    return Results.Created($"/users/{user.Id}", user);
});

app.MapPut("/users/{id:guid}", (Guid id, UserInput input, UserStore store) =>
{
    var errors = Validate(input);
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    var user = store.Update(id, input);
    return user is null ? Results.NotFound(new { error = "User not found." }) : Results.Ok(user);
});

app.MapDelete("/users/{id:guid}", (Guid id, UserStore store) =>
    store.Delete(id) ? Results.NoContent() : Results.NotFound(new { error = "User not found." }));

app.Run();

static Dictionary<string, string[]> Validate(UserInput input)
{
    var results = new List<ValidationResult>();
    var context = new ValidationContext(input);
    Validator.TryValidateObject(input, context, results, validateAllProperties: true);
    return results
        .SelectMany(result => result.MemberNames.DefaultIfEmpty("User"),
            (result, member) => new { member, message = result.ErrorMessage ?? "Invalid value." })
        .GroupBy(item => item.member)
        .ToDictionary(group => group.Key, group => group.Select(item => item.message).ToArray());
}

public sealed record UserRecord(Guid Id, string Name, string Email);

public sealed class UserInput
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; init; } = "";

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = "";
}

public sealed class UserStore
{
    private readonly ConcurrentDictionary<Guid, UserRecord> _users = new();

    public UserRecord[] GetAll() => _users.Values.OrderBy(user => user.Name).ToArray();
    public UserRecord? Get(Guid id) => _users.TryGetValue(id, out var user) ? user : null;

    public UserRecord Create(UserInput input)
    {
        var user = new UserRecord(Guid.NewGuid(), input.Name.Trim(), input.Email.Trim());
        _users[user.Id] = user;
        return user;
    }

    public UserRecord? Update(Guid id, UserInput input)
    {
        if (!_users.ContainsKey(id)) return null;
        var updated = new UserRecord(id, input.Name.Trim(), input.Email.Trim());
        return _users.TryUpdate(id, updated, _users[id]) ? updated : Get(id);
    }

    public bool Delete(Guid id) => _users.TryRemove(id, out _);
}

public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Internal server error." });
        }
    }
}

public sealed class TokenAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // The health/root endpoint is public; protect all user-management routes.
        if (context.Request.Path == "/" || !context.Request.Path.StartsWithSegments("/users"))
        {
            await next(context);
            return;
        }

        var expectedToken = configuration["API_TOKEN"];
        var authorization = context.Request.Headers.Authorization.ToString();
        var providedToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim()
            : "";

        if (string.IsNullOrWhiteSpace(expectedToken) ||
            !string.Equals(providedToken, expectedToken, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized." });
            return;
        }

        await next(context);
    }
}

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);
        logger.LogInformation("{Method} {Path} responded {StatusCode}",
            context.Request.Method, context.Request.Path, context.Response.StatusCode);
    }
}
