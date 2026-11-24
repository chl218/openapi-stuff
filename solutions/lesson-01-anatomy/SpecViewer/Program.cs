// Serves a hand-written openapi.yaml in Swagger UI. No code-first generation here:
// the YAML is the only source of truth.
//
//   dotnet run         then open http://localhost:5000/swagger   (see the console for the port)
//
// Edit ../openapi.yaml and refresh the page. No rebuild needed.

// Create the web app builder (reads config, sets up logging and the web server).
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Prefer the file next to the project so edits show up immediately.
// ContentRootPath is the project folder when you use `dotnet run`.
var live = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "openapi.yaml"));

// Fall back to the copy that the build placed beside the compiled program.
var specPath = File.Exists(live) ? live : Path.Combine(AppContext.BaseDirectory, "openapi.yaml");
Console.WriteLine($"Serving spec from: {specPath}");

// Serve the YAML text. It is read on every request, which is why refresh-after-edit works.
app.MapGet("/openapi.yaml", () => Results.Text(File.ReadAllText(specPath), "application/yaml"));

// Convenience: opening the root URL sends you straight to the docs page.
app.MapGet("/", () => Results.Redirect("/swagger"));

// Add the Swagger UI page (at /swagger) and tell it which spec file to display.
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi.yaml", "Library API (hand-written)"));

// Start listening for requests.
app.Run();
