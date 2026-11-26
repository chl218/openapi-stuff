// Primer B: the parts of an ASP.NET Core server, in the order they run.
//
//   1. builder   : configuration, logging, and services (dependency injection)
//   2. app       : the built server
//   3. middleware: code every request passes through, in order
//   4. endpoints : URL + HTTP method -> handler
//   5. Run       : start Kestrel and listen
//
// Run it:  dotnet run   then try the requests listed in requests.http

using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;   // typed results: Ok<T>, NotFound, Created<T>, ...

// ---------- 1. builder ----------
var builder = WebApplication.CreateBuilder(args);
// CreateBuilder has already loaded configuration from (later wins):
//   appsettings.json -> appsettings.{Environment}.json -> user secrets (Development)
//   -> environment variables -> command-line args

// Register services. The container creates them and hands them to whoever asks.
builder.Services.AddSingleton<ShelfStore>();                 // one instance for the whole app
builder.Services.AddScoped<RequestStamp>();                  // one instance per HTTP request
builder.Services.AddProblemDetails();                        // standard JSON error bodies (RFC 9457)

// Bind the "Library" section of configuration to a typed class.
builder.Services.Configure<LibraryOptions>(builder.Configuration.GetSection("Library"));

// ---------- 2. app ----------
var app = builder.Build();

// ---------- 3. middleware (order matters: top runs first on the way in, last on the way out) ----------
app.UseExceptionHandler();      // turns an unhandled exception into a 500 Problem Details response
app.UseStatusCodePages();       // gives bare 404/405 responses a Problem Details body too

// Our own middleware: time every request and add the result as a response header.
app.Use(async (context, next) =>
{
    var watch = Stopwatch.StartNew();
    context.Response.OnStarting(() =>   // headers must be set before the body starts
    {
        context.Response.Headers["X-Elapsed-Ms"] = watch.ElapsedMilliseconds.ToString();
        return Task.CompletedTask;
    });
    await next(context);   // hand over to the next middleware (and finally the endpoint)
    app.Logger.LogInformation("{Method} {Path} -> {Status} in {Ms} ms",
        context.Request.Method, context.Request.Path, context.Response.StatusCode, watch.ElapsedMilliseconds);
});

// ---------- 4. endpoints ----------

// Configuration: read a value from appsettings.json via IConfiguration.
app.MapGet("/", (IConfiguration config) => config["Library:Greeting"] ?? "Hello");

// Route parameter {name} and an optional query string ?shout=true.
app.MapGet("/hello/{name}", (string name, bool? shout) =>
{
    var text = $"Hello, {name}!";
    return shout == true ? text.ToUpperInvariant() : text;
});

// Typed options + a scoped service + a header, all injected by parameter type or attribute.
app.MapGet("/whoami", (
    Microsoft.Extensions.Options.IOptions<LibraryOptions> options,
    RequestStamp stamp,
    [Microsoft.AspNetCore.Mvc.FromHeader(Name = "User-Agent")] string? userAgent) =>
    new { library = options.Value.Name, requestId = stamp.Id, userAgent });

// A route group: shared prefix for several endpoints.
var shelf = app.MapGroup("/shelf");

// GET /shelf?max=2 : a query parameter with a default value.
shelf.MapGet("/", (ShelfStore store, int max = 50) => TypedResults.Ok(store.All().Take(max)));

// GET /shelf/3 : {id:int} is a route constraint; /shelf/abc does not match and gives 404.
shelf.MapGet("/{id:int}", Results<Ok<Book>, NotFound> (int id, ShelfStore store) =>
    store.Find(id) is { } book ? TypedResults.Ok(book) : TypedResults.NotFound());

// POST /shelf with a JSON body: complex types are read from the body automatically.
shelf.MapPost("/", Results<Created<Book>, ValidationProblem> (NewBook input, ShelfStore store) =>
{
    if (string.IsNullOrWhiteSpace(input.Title))
        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title"] = ["Title is required."],
        });
    var book = store.Add(input);
    return TypedResults.Created($"/shelf/{book.Id}", book);   // 201 + Location header
});

// Throws on purpose, to show UseExceptionHandler at work.
app.MapGet("/boom", string () => throw new InvalidOperationException("Something broke."));

// Environments: this endpoint only exists when ASPNETCORE_ENVIRONMENT=Development.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/debug/env", (IWebHostEnvironment env) =>
        new { env.EnvironmentName, env.ApplicationName, env.ContentRootPath });
}

// ---------- 5. Run ----------
app.Run();   // blocks until you press Ctrl+C

// ---------- types ----------

// JSON uses camelCase by default: Title in C# becomes "title" on the wire.
public record Book(int Id, string Title, int? Year);
public record NewBook(string Title, int? Year);

// Bound from the "Library" section of appsettings.json.
public class LibraryOptions
{
    public string Name { get; set; } = "Unnamed library";
    public string Greeting { get; set; } = "Hello";
}

// One per request: every service that asks for it during the same request gets the same Id.
public class RequestStamp
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
}

// One for the whole app. Requests run in parallel, so changes are locked.
public class ShelfStore
{
    private readonly List<Book> _books = [new(1, "Dune", 1965), new(2, "Neuromancer", 1984)];
    private readonly Lock _gate = new();
    private int _nextId = 3;

    public Book[] All() { lock (_gate) return [.. _books]; }

    public Book? Find(int id) { lock (_gate) return _books.FirstOrDefault(b => b.Id == id); }

    public Book Add(NewBook input)
    {
        lock (_gate)
        {
            var book = new Book(_nextId++, input.Title, input.Year);
            _books.Add(book);
            return book;
        }
    }
}
