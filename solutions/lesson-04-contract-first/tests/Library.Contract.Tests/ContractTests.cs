using Library.Contract.Tests.Generated;          // LibraryClient, NewBook, Problem, ApiException (all generated)
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Library.Contract.Tests;

// IClassFixture gives each test class one shared in-memory copy of the server.
public class ContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly LibraryClient _client;

    public ContractTests(WebApplicationFactory<Program> factory)
    {
        // An HttpClient wired directly to the in-memory server (no real network involved).
        var http = factory.CreateClient();
        // The contract's server URL is http://localhost:5000; point the client at the in-memory server instead.
        _client = new LibraryClient(http) { BaseUrl = http.BaseAddress!.ToString() };
    }

    // The server's store is shared by all tests, so give each test its own ISBN to avoid clashes.
    // This builds a 13-digit string from a GUID's digits, padded with zeros if needed.
    private static string NewIsbn() => string.Concat(Guid.NewGuid().ToString("N").Where(char.IsDigit).Concat("0000000000000").Take(13));

    [Fact]
    public async Task Create_then_get_returns_same_book()
    {
        var isbn = NewIsbn();
        // Typed call: no URLs or JSON by hand. This line only compiles if the contract has CreateBook(NewBook).
        var created = await _client.CreateBookAsync(new NewBook { Title = "Refactoring", Isbn = isbn, Year = 2018 });
        Assert.True(created.Id > 0);   // the server assigned an id

        var fetched = await _client.GetBookAsync(created.Id);
        Assert.Equal("Refactoring", fetched.Title);
        Assert.Equal(isbn, fetched.Isbn);
    }

    [Fact]
    public async Task Duplicate_isbn_returns_conflict()
    {
        var isbn = NewIsbn();
        await _client.CreateBookAsync(new NewBook { Title = "A", Isbn = isbn });

        // Documented error responses become exceptions in the generated client.
        // ApiException<Problem> carries the status code and the parsed error body.
        var ex = await Assert.ThrowsAsync<ApiException<Problem>>(
            () => _client.CreateBookAsync(new NewBook { Title = "B", Isbn = isbn }));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Missing_book_returns_not_found_with_problem_body()
    {
        var ex = await Assert.ThrowsAsync<ApiException<Problem>>(() => _client.GetBookAsync(99999));
        Assert.Equal(404, ex.StatusCode);
        Assert.False(string.IsNullOrEmpty(ex.Result.Title));   // the error body has the shared Problem shape
    }
}
