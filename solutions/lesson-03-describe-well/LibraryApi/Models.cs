using System.ComponentModel.DataAnnotations;   // [Required], [StringLength], [Range], ...
using Microsoft.AspNetCore.Mvc;                // ProblemDetails, the standard error body

// The /// comments below are XML doc comments. With GenerateDocumentationFile turned on
// in the .csproj, .NET 10 copies them into the OpenAPI document as descriptions.

/// <summary>A book in the library.</summary>
/// <param name="Id">Assigned by the server.</param>
/// <param name="Title">Title as printed on the cover.</param>
/// <param name="Isbn">ISBN-13, hyphens optional.</param>
/// <param name="Year">Year of first publication.</param>
public record Book(int Id, string Title, string Isbn, int? Year);

/// <summary>The fields a caller sends to add a book.</summary>
public class NewBook
{
    /// <summary>Title as printed on the cover.</summary>
    // Required + length 1..200. These show up in the spec as minLength / maxLength.
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = "";

    /// <summary>ISBN-13, hyphens optional.</summary>
    // The pattern means: 13 digits, each optionally followed by a hyphen. Shows up in the spec as "pattern".
    [Required, RegularExpression(@"^(?:\d-?){12}\d$", ErrorMessage = "ISBN must be 13 digits; hyphens are allowed.")]
    public string Isbn { get; set; } = "";

    /// <summary>Year of first publication.</summary>
    // Optional (int?), but if present it must be 1450..2100. Shows up as minimum / maximum.
    [Range(1450, 2100)]
    public int? Year { get; set; }
}

public static class Rules
{
    /// <summary>Returns null when valid, otherwise a field -> messages map.</summary>
    // Minimal APIs do not check [Required] and friends by themselves, so we run the checks ourselves.
    public static Dictionary<string, string[]>? Validate(object model)
    {
        var results = new List<ValidationResult>();

        // Runs every validation attribute on the object's properties and collects the failures.
        if (Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
            return null;   // null means "no errors"

        // Turn the failure list into { "title": ["..."], "isbn": ["..."] }, the shape a 400 response uses.
        return results
            .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [""])           // which field(s) failed
                .Select(m => (Field: m, Message: r.ErrorMessage ?? "Invalid value.")))
            .GroupBy(x => x.Field)                                                  // collect all messages per field
            .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());
    }

    // Builds the one error shape every 4xx response uses (RFC 9457 "Problem Details").
    public static ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Type = $"https://httpstatuses.com/{status}",   // a URL that identifies the kind of problem
        Title = title,                                 // short, the same for every occurrence
        Status = status,                               // the HTTP status code
        Detail = detail,                               // specific to this occurrence
    };
}
