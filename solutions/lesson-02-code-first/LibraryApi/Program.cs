using Microsoft.AspNetCore.Http.HttpResults;   // typed results: Ok<T>, NotFound, Created<T>, ...

var builder = WebApplication.CreateBuilder(args);

// Turn on OpenAPI generation. At startup ASP.NET Core inspects every endpoint
// below and builds a document describing them.
builder.Services.AddOpenApi();

var app = builder.Build();

// Only expose the docs while developing.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                                                    // serves the generated document at /openapi/v1.json
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "v1"));  // the clickable page at /swagger
}

// A fake database: a list in memory. It resets whenever the app restarts.
var books = new List<Book>();
var nextId = 1;
var gate = new object();   // requests run in parallel, so we lock around changes to the list

// Group all book endpoints under /books. WithTags groups them together in Swagger UI.
var api = app.MapGroup("/books").WithTags("Books");

// GET /books : return every book.
api.MapGet("/", () =>
{
    lock (gate) return TypedResults.Ok(books.ToArray());   // Ok = HTTP 200 with the array as JSON
}).WithName("listBooks");   // WithName sets the operationId in the spec

// GET /books/{id} : return one book.
// The return type Results<Ok<Book>, NotFound> tells the spec both outcomes: 200 with a Book, or 404.
api.MapGet("/{id:int}", Results<Ok<Book>, NotFound> (int id) =>
{
    lock (gate)
    {
        var book = books.FirstOrDefault(b => b.Id == id);
        return book is null ? TypedResults.NotFound() : TypedResults.Ok(book);
    }
}).WithName("getBook");

// POST /books : add a book. ASP.NET Core reads the JSON request body into a NewBook.
api.MapPost("/", (NewBook input) =>
{
    lock (gate)
    {
        var book = new Book(nextId++, input.Title, input.Isbn, input.Year);   // the server assigns the id
        books.Add(book);
        return TypedResults.Created($"/books/{book.Id}", book);               // HTTP 201 plus a Location header
    }
}).WithName("createBook");

// DELETE /books/{id} : remove a book.
api.MapDelete("/{id:int}", Results<NoContent, NotFound> (int id) =>
{
    lock (gate)
    {
        var removed = books.RemoveAll(b => b.Id == id) > 0;   // RemoveAll returns how many were removed
        return removed ? TypedResults.NoContent() : TypedResults.NotFound();   // 204 or 404
    }
}).WithName("deleteBook");

app.Run();

// A record is a short way to declare a class with read-only properties.
// These two shapes become the "Book" and "NewBook" schemas in the OpenAPI document.
public record Book(int Id, string Title, string Isbn, int? Year);          // what the API returns
public record NewBook(string Title, string Isbn, int? Year);               // what a caller sends
