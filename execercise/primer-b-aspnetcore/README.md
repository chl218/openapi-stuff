# Primer B — ASP.NET Core server crash course

Goal: build a small server by hand and know what each line of `Program.cs` is for, so Lesson 2 is about OpenAPI rather than about ASP.NET Core.

## Files
- `project-setup.pseudo`: commands and files to create.
- `program.pseudo`: what Program.cs must do, in order.

## Run it
1. Follow `project-setup.pseudo` to create `HelloApi/`.
2. Implement `program.pseudo` in C#.
3. `dotnet run`, then call each endpoint (browser, curl, or a `.http` file).

## Done when
- `GET /` returns the greeting from `appsettings.Development.json`.
- `GET /shelf/abc` gives 404 without your handler running (route constraint).
- `POST /shelf` with `{"title":""}` returns 400 with an `errors.title` entry.
- `GET /boom` returns a 500 JSON body, not an HTML error page or a stack trace.
- Every response has an `X-Elapsed-Ms` header.
- Run in Production and `/debug/env` is gone:
  `dotnet run --environment Production`
- Override the greeting without editing a file:
  - bash: `Library__Greeting="hi" dotnet run`
  - PowerShell: `$env:Library__Greeting="hi"; dotnet run`

## Stretch
- `GET /boom` has the `X-Elapsed-Ms` header, but the console has no log line for it. Why? Move the timing middleware above `UseExceptionHandler` and check again.
- Register `ShelfStore` as scoped instead of singleton. POST a book, then GET the shelf. Where did it go?
