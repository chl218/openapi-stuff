using Library.Server.Generated;     // the generated base class and models (Book, NewBook, Problem)
using Microsoft.AspNetCore.Mvc;

namespace Library.Server;

// The base class and the models (Book, NewBook, Problem) come from Generated/Controllers.g.cs.
// If your generator version produces different method signatures, open that file and match them.
//
// The generated base class already has the routes ([HttpGet("books")] etc.).
// All we do here is override each abstract method with the real behaviour.
// If the contract changes (say a field is renamed), this class stops compiling until you update it.
public class BooksController : BooksControllerBase
{
    // Static so state survives between requests (ASP.NET Core creates a new controller per request).
    private static readonly object Gate = new();       // protects the list from parallel requests
    private static readonly List<Book> Store = new();  // fake in-memory database
    private static int _nextId = 1;

    // GET /books?limit=N  (operationId: listBooks)
    public override Task<ActionResult<ICollection<Book>>> ListBooks(int? limit)
    {
        var n = limit ?? 20;   // the contract says default 20
        lock (Gate)
        {
            ICollection<Book> page = Store.Take(n).ToList();
            // Ok(...) = HTTP 200. The generated signature is async, so wrap the result in a completed Task.
            return Task.FromResult<ActionResult<ICollection<Book>>>(Ok(page));
        }
    }

    // POST /books  (operationId: createBook)
    public override Task<ActionResult<Book>> CreateBook(NewBook body)
    {
        lock (Gate)
        {
            // The contract documents a 409 for a duplicate ISBN, so we must return it.
            if (Store.Any(b => b.Isbn == body.Isbn))
                return Task.FromResult<ActionResult<Book>>(Conflict(new Problem
                {
                    Title = "ISBN already exists",
                    Status = 409,
                    Detail = $"A book with ISBN {body.Isbn} is already in the library.",
                }));

            // Generated models use settable properties, so we build the Book with an object initializer.
            var book = new Book { Id = _nextId++, Title = body.Title, Isbn = body.Isbn, Year = body.Year };
            Store.Add(book);
            return Task.FromResult<ActionResult<Book>>(Created($"/books/{book.Id}", book));   // HTTP 201
        }
    }

    // GET /books/{id}  (operationId: getBook)
    public override Task<ActionResult<Book>> GetBook(int id)
    {
        lock (Gate)
        {
            var book = Store.FirstOrDefault(b => b.Id == id);
            return Task.FromResult<ActionResult<Book>>(book is null
                ? NotFound(new Problem { Title = "Book not found", Status = 404, Detail = $"No book has id {id}." })   // documented 404
                : Ok(book));
        }
    }

    // DELETE /books/{id}  (operationId: deleteBook)
    public override Task<IActionResult> DeleteBook(int id)
    {
        lock (Gate)
        {
            if (Store.RemoveAll(b => b.Id == id) == 0)
                return Task.FromResult<IActionResult>(NotFound(new Problem { Title = "Book not found", Status = 404, Detail = $"No book has id {id}." }));
            return Task.FromResult<IActionResult>(NoContent());   // HTTP 204, no body
        }
    }
}
