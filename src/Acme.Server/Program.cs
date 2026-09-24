using Acme.Server.Api;
using Acme.Server.Contracts;
using Acme.Server.Data;
using Acme.Server.Data.Dialect;
using Acme.Server.Data.Repositories;
using Acme.Server.Domain;
using Acme.Server.Security;
using Acme.Server.Seed;
using Acme.Server.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

// A minimal console-only logger, live for the brief window between process start and
// the full configuration below -- so a failure while building that configuration still
// gets logged somewhere instead of being lost.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Built eagerly, not through UseSerilog's lazy-configuration overload: that overload
    // wraps a ReloadableLogger that freezes on its first host Build() call, and
    // WebApplicationFactory (see AcmeApiFactory) builds the host twice per test run.
    var logDirectory = Path.Combine(builder.Environment.ContentRootPath, "Logs");

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Is(builder.Environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        // One line per HTTP request (method, path, status, elapsed), written by
        // UseSerilogRequestLogging below -- every access, on its own file.
        .WriteTo.Logger(access => access
            .Filter.ByIncludingOnly(Matching.FromSource("Serilog.AspNetCore.RequestLoggingMiddleware"))
            .WriteTo.File(
                Path.Combine(logDirectory, "access-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31))
        // Everything logged at Warning or above -- including ApiExceptionHandler's
        // unhandled exceptions -- lands here too, so an on-call engineer has one file to
        // tail regardless of which part of the app raised it.
        .WriteTo.Logger(errors => errors
            .MinimumLevel.Warning()
            .WriteTo.File(
                Path.Combine(logDirectory, "errors-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31))
        .CreateLogger();

    builder.Host.UseSerilog();

    // ---- configuration --------------------------------------------------------------

    builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

    // The one place that maps a configured provider onto a dialect. Every other part of
    // the application talks to ISqlDialect and never asks which engine it is.
    builder.Services.AddSingleton<ISqlDialect>(sp =>
    {
        var provider = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.Provider;
        return provider switch
        {
            DbProvider.Sqlite => new SqliteDialect(),
            DbProvider.Postgres => new PostgresDialect(),
            DbProvider.MySql => new MySqlDialect(),
            DbProvider.SqlServer => new SqlServerDialect(),
            _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'."),
        };
    });

    builder.Services.AddSingleton(TimeProvider.System);

    // ---- data + services ------------------------------------------------------------

    builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
    builder.Services.AddSingleton<IProductRepository, ProductRepository>();
    builder.Services.AddSingleton<ICustomerRepository, CustomerRepository>();
    builder.Services.AddSingleton<IOrderRepository, OrderRepository>();
    builder.Services.AddSingleton<IAppUserRepository, AppUserRepository>();
    builder.Services.AddSingleton<DatabaseMigrator>();

    builder.Services.AddScoped<ProductService>();
    builder.Services.AddScoped<CustomerService>();
    builder.Services.AddScoped<OrderService>();
    builder.Services.AddScoped<SignInService>();
    builder.Services.AddScoped<DevUserSeeder>();
    builder.Services.AddScoped<DevDataSeeder>();

    // ---- health -----------------------------------------------------------------------

    builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

    // ---- security -------------------------------------------------------------------

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IAccessGuard, AccessGuard>();
    builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = "acme.auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);

            // This is an API, not a set of pages: answer with a status code rather than
            // redirecting to an HTML sign-in form. The SPA turns 401 into its login route
            // and 403 into its access-denied route.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

    // The two @PreAuthorize expressions, as policies. Enforcement happens in the services
    // via IAccessGuard, not on the endpoints -- see AccessGuard for why.
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(Role.Admin.ToDbValue()))
        .AddPolicy(Policies.AdminOrStaff, policy =>
            policy.RequireRole(Role.Admin.ToDbValue(), Role.Staff.ToDbValue()));

    builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

    // ---- HTTP -----------------------------------------------------------------------

    builder.Services.AddValidation();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new OrderStatusJsonConverter());
        options.SerializerOptions.Converters.Add(new RoleJsonConverter());
    });

    var app = builder.Build();

    // ---- startup work ---------------------------------------------------------------

    var databaseOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

    if (databaseOptions.MigrateOnStartup || args.Contains("--migrate"))
    {
        app.Services.GetRequiredService<DatabaseMigrator>().Run();
    }

    // Closes the gap Acme flagged as unsolved: outside development there was no way to
    // create a first account at all.
    if (args.Contains("--create-admin"))
    {
        await AdminProvisioning.RunAsync(app.Services, args);
        return;
    }

    if (app.Configuration.GetValue<bool>("Seed:Enabled"))
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DevUserSeeder>().RunAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<DevDataSeeder>().RunAsync(CancellationToken.None);
    }

    // ---- pipeline -------------------------------------------------------------------

    app.UseExceptionHandler();

    // One line per request into Logs/access-*.log, via the filtered sub-logger above.
    app.UseSerilogRequestLogging();

    // Serves the built SPA in production; in development Vite serves it and proxies /api
    // here, so these are no-ops.
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealth();
    app.MapAuth();
    app.MapDashboard();
    app.MapProducts();
    app.MapCustomers();
    app.MapOrders();

    // Client-side routing: anything that is not an API call renders the SPA shell.
    app.MapFallbackToFile("index.html");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Acme.Server terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposed so the integration tests can build the same application.</summary>
public partial class Program;
