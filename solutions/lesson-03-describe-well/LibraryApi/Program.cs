using Microsoft.AspNetCore.Http.HttpResults;   // typed results (Ok<T>, Created<T>, ...)
using Microsoft.AspNetCore.Mvc;                // ProblemDetails

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();   // makes unhandled errors use the standard Problem Details shape
builder.Services.AddOpenApi();          // generate the OpenAPI document

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                                                    // /openapi/v1.json
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "v1"));  // /swagger
}

// In-memory storage, same as Lesson 2.
var books = new List<Book>();
var nextId = 1;
var gate = new object();

var api = app.MapGroup("/books").WithTags("Books");

// GET /books?limit=N
// The return type lists every outcome, so each one appears in the spec:
//   Ok<Book[]> = 200 with a list;  ValidationProblem = 400 with per-field errors.
api.MapGet("/", Results<Ok<Book[]>, ValidationProblem> (int? limit) =>
{
    var n = limit ?? 20;   // default page size
    if (n is < 1 or > 100)
        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["limit"] = ["limit must be between 1 and 100."],
        });

    lock (gate) return TypedResults.Ok(books.Take(n).ToArray());
})
.WithName("listBooks")                // operationId
.WithSummary("List books")            // one-line title in the docs
.WithDescription("Returns up to `limit` books (default 20). A limit outside 1-100 returns 400.");   // longer text

// GET /books/{id}
// NotFound<ProblemDetails> = 404 whose body has the shared error shape.
api.MapGet("/{id:int}", Results<Ok<Book>, NotFound<ProblemDetails>> (int id) =>
{
    lock (gate)
    {
        var book = books.FirstOrDefault(b => b.Id == id);
        return book is null
            ? TypedResults.NotFound(Rules.Problem(404, "Book not found", $"No book has id {id}."))
            : TypedResults.Ok(book);
    }
})
.WithName("getBook")
.WithSummary("Get one book");

// POST /books : three possible outcomes, all documented through the return type:
//   201 Created, 400 validation errors, 409 the ISBN already exists.
api.MapPost("/", Results<Created<Book>, ValidationProblem, Conflict<ProblemDetails>> (NewBook input) =>
{
    // Check [Required], [StringLength], [Range] ... and return 400 listing each bad field.
    if (Rules.Validate(input) is { } errors)
        return TypedResults.ValidationProblem(errors);

    lock (gate)
    {
        // ISBN must be unique -> 409 Conflict otherwise.
        if (books.Any(b => b.Isbn == input.Isbn))
            return TypedResults.Conflict(Rules.Problem(409, "ISBN already exists", $"A book with ISBN {input.Isbn} is already in the library."));

        var book = new Book(nextId++, input.Title, input.Isbn, input.Year);
        books.Add(book);
        return TypedResults.Created($"/books/{book.Id}", book);
    }
})
.WithName("createBook")
.WithSummary("Add a book")
.WithDescription("400 lists each invalid field. 409 means the ISBN is already in the library.");

// DELETE /books/{id} : 204 when removed, 404 when it never existed.
api.MapDelete("/{id:int}", Results<NoContent, NotFound<ProblemDetails>> (int id) =>
{
    lock (gate)
    {
        return books.RemoveAll(b => b.Id == id) > 0
            ? TypedResults.NoContent()
            : TypedResults.NotFound(Rules.Problem(404, "Book not found", $"No book has id {id}."));
    }
})
.WithName("deleteBook")
.WithSummary("Remove a book");

app.Run();

// Lets test projects use WebApplicationFactory<Program> to start this app in memory.
public partial class Program { }
