var builder = WebApplication.CreateBuilder(args);

// This lesson uses controller classes (not minimal API lambdas) because the generator
// produces an abstract controller for us to fill in.
builder.Services.AddControllers();

var app = builder.Build();

// Find every controller class in the project and route requests to it.
app.MapControllers();

// Note: there is NO AddOpenApi() here. The YAML in contracts/ is the document.

// Optional: serve the contract itself so a viewer can load it.
// app.MapGet("/contract/library.yaml", () => Results.File(
//     Path.Combine(app.Environment.ContentRootPath, "../../contracts/library.yaml"), "application/yaml"));

app.Run();

// Lets test projects use WebApplicationFactory<Program> to start this app in memory.
public partial class Program { }
