using TyfloCentrum.PushService.Contracts;
using TyfloCentrum.PushService.Models;
using TyfloCentrum.PushService.Options;
using TyfloCentrum.PushService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PushServiceOptions>(builder.Configuration.GetSection("PushService"));
builder.Services.AddSingleton<PushStateStore>();
builder.Services.AddSingleton<PushNotificationDispatcher>();
builder.Services.AddSingleton<WordPressPollingCoordinator>();
builder.Services.AddHostedService<WordPressPollingService>();
builder.Services.AddHttpClient<WordPressFeedClient>();
builder.Services.AddHttpClient<WnsAccessTokenProvider>();
builder.Services.AddHttpClient<IWnsNotificationSender, WnsNotificationSender>();

var app = builder.Build();

app.MapGet(
    "/health",
    () => Results.Ok(new
    {
        ok = true,
        name = "tyflocentrum-push-windows",
        version = "0.1.0",
        time = DateTimeOffset.UtcNow.ToString("O"),
    })
);

app.MapPost(
    "/api/v1/register",
    async (
        RegisterRequest request,
        PushStateStore stateStore,
        CancellationToken cancellationToken
    ) =>
    {
        if (!IsValidToken(request.Token, request.Env))
        {
            return Results.BadRequest(new { ok = false, error = "Invalid token" });
        }

        var nowIso = DateTimeOffset.UtcNow.ToString("O");
        var env = NormalizeEnv(request.Env);
        var prefs = PushNotificationPreferences.Normalize(request.Prefs);

        await stateStore.UpdateAsync(
            state =>
            {
                if (!state.Tokens.TryGetValue(request.Token, out var record))
                {
                    record = new PushSubscriberRecord
                    {
                        Token = request.Token.Trim(),
                        CreatedAt = nowIso,
                    };
                    state.Tokens[request.Token] = record;
                }

                record.Env = env;
                record.Prefs = prefs;
                record.UpdatedAt = nowIso;
                record.LastSeenAt = nowIso;
            },
            cancellationToken
        );

        return Results.Ok(new { ok = true });
    }
);

app.MapPost(
    "/api/v1/update",
    async (
        UpdateRequest request,
        PushStateStore stateStore,
        CancellationToken cancellationToken
    ) =>
    {
        if (!IsValidToken(request.Token, env: null))
        {
            return Results.BadRequest(new { ok = false, error = "Invalid token" });
        }

        var snapshot = await stateStore.ReadAsync(cancellationToken);
        if (!snapshot.Tokens.ContainsKey(request.Token))
        {
            return Results.NotFound(new { ok = false, error = "Unknown token" });
        }

        var nowIso = DateTimeOffset.UtcNow.ToString("O");
        var prefs = PushNotificationPreferences.Normalize(request.Prefs);
        await stateStore.UpdateAsync(
            state =>
            {
                if (state.Tokens.TryGetValue(request.Token, out var record))
                {
                    record.Prefs = prefs;
                    record.UpdatedAt = nowIso;
                    record.LastSeenAt = nowIso;
                }
            },
            cancellationToken
        );

        return Results.Ok(new { ok = true });
    }
);

app.MapPost(
    "/api/v1/unregister",
    async (
        UnregisterRequest request,
        PushStateStore stateStore,
        CancellationToken cancellationToken
    ) =>
    {
        if (!IsValidToken(request.Token, env: null))
        {
            return Results.BadRequest(new { ok = false, error = "Invalid token" });
        }

        await stateStore.UpdateAsync(
            state =>
            {
                state.Tokens.Remove(request.Token);
            },
            cancellationToken
        );
        return Results.Ok(new { ok = true });
    }
);

app.MapPost(
    "/api/v1/events/live-start",
    async (
        HttpRequest httpRequest,
        EventNotificationRequest request,
        Microsoft.Extensions.Options.IOptions<PushServiceOptions> options,
        WordPressPollingCoordinator coordinator,
        CancellationToken cancellationToken
    ) =>
    {
        if (!IsAuthorized(httpRequest, options.Value))
        {
            return Results.Text("Forbidden", statusCode: StatusCodes.Status403Forbidden);
        }

        await coordinator.DispatchEventAsync(
            PushCategories.Live,
            new PushDispatchPayload(
                PushCategories.Live,
                string.IsNullOrWhiteSpace(request.Title) ? "Audycja na żywo" : request.Title.Trim(),
                "Tyfloradio nadaje teraz na żywo."
            ),
            cancellationToken
        );

        return Results.Ok(new { ok = true });
    }
);

app.MapPost(
    "/api/v1/events/live-end",
    (HttpRequest httpRequest, Microsoft.Extensions.Options.IOptions<PushServiceOptions> options) =>
    {
        if (!IsAuthorized(httpRequest, options.Value))
        {
            return Results.Text("Forbidden", statusCode: StatusCodes.Status403Forbidden);
        }

        return Results.Ok(new { ok = true });
    }
);

app.MapPost(
    "/api/v1/events/schedule-updated",
    async (
        HttpRequest httpRequest,
        EventNotificationRequest request,
        Microsoft.Extensions.Options.IOptions<PushServiceOptions> options,
        WordPressPollingCoordinator coordinator,
        CancellationToken cancellationToken
    ) =>
    {
        if (!IsAuthorized(httpRequest, options.Value))
        {
            return Results.Text("Forbidden", statusCode: StatusCodes.Status403Forbidden);
        }

        await coordinator.DispatchEventAsync(
            PushCategories.Schedule,
            new PushDispatchPayload(
                PushCategories.Schedule,
                "Zaktualizowano ramówkę",
                string.IsNullOrWhiteSpace(request.UpdatedAt)
                    ? "Sprawdź najnowszy plan audycji Tyfloradia."
                    : $"Sprawdź najnowszy plan audycji. Aktualizacja: {request.UpdatedAt.Trim()}."
            ),
            cancellationToken
        );

        return Results.Ok(new { ok = true });
    }
);

app.Run();

static bool IsValidToken(string? token, string? env)
{
    if (string.IsNullOrWhiteSpace(token))
    {
        return false;
    }

    var normalizedToken = token.Trim();
    if (normalizedToken.Length > 4096)
    {
        return false;
    }

    if (!string.Equals(env, "windows-wns", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return Uri.TryCreate(normalizedToken, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

static string NormalizeEnv(string? env)
{
    return string.IsNullOrWhiteSpace(env) ? "unknown" : env.Trim().ToLowerInvariant();
}

static bool IsAuthorized(HttpRequest request, PushServiceOptions options)
{
    if (string.IsNullOrWhiteSpace(options.WebhookSecret))
    {
        return false;
    }

    if (!request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var providedSecret = request.Headers.Authorization.ToString()["Bearer ".Length..].Trim();
    return string.Equals(providedSecret, options.WebhookSecret, StringComparison.Ordinal);
}
