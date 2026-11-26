// YamlCheck: parse a YAML file and print what the parser actually understood, as JSON.
//
// Why JSON? JSON has no hidden typing rules. Every string is in quotes and every number is not,
// so you can see at a glance whether `version: 1.10` became the string "1.10" or the number 1.1.
//
// Usage:  dotnet run --project YamlCheck -- path/to/file.yaml

using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: YamlCheck <file.yaml>");
    return 2;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"not found: {path}");
    return 2;
}

var text = File.ReadAllText(path);

// Check for tabs first. YAML forbids tabs in indentation, and the parser's own
// error for this points at line 1, which is no help. So find the real line ourselves.
var lines = text.Split('\n');
for (var n = 0; n < lines.Length; n++)
{
    var indent = lines[n].Length - lines[n].TrimStart(' ', '\t').Length;
    if (lines[n][..indent].Contains('\t'))
    {
        Console.Error.WriteLine($"YAML error at line {n + 1}: indented with a tab. YAML allows spaces only.");
        return 1;
    }
}

var stream = new YamlStream();
try
{
    stream.Load(new StringReader(text));   // throws on syntax errors (bad indentation, unclosed quotes, ...)
}
catch (YamlException ex)
{
    // ex.Start tells you the line and column where the parser gave up.
    Console.Error.WriteLine($"YAML error at line {ex.Start.Line}, column {ex.Start.Column}:");
    Console.Error.WriteLine($"  {ex.InnerException?.Message ?? ex.Message}");
    return 1;
}

if (stream.Documents.Count == 0)
{
    Console.WriteLine("(empty document)");
    return 0;
}

// A file can hold several documents separated by "---". Print each one.
foreach (var doc in stream.Documents)
{
    var json = ToJson(doc.RootNode);
    Console.WriteLine(json?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "null");
}
return 0;

// Turn a YAML node into a JSON node, applying the YAML 1.2 "core schema" rules
// that decide whether an unquoted value is a string, number, boolean, or null.
static JsonNode? ToJson(YamlNode node) => node switch
{
    YamlMappingNode map => new JsonObject(map.Children.Select(kv =>
        KeyValuePair.Create(((YamlScalarNode)kv.Key).Value ?? "", ToJson(kv.Value)))),
    YamlSequenceNode seq => new JsonArray(seq.Children.Select(ToJson).ToArray()),
    YamlScalarNode scalar => Scalar(scalar),
    _ => null,
};

static JsonNode? Scalar(YamlScalarNode s)
{
    var text = s.Value ?? "";

    // Anything quoted, or written as a | or > block, is always a string.
    if (s.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted
                or ScalarStyle.Literal or ScalarStyle.Folded)
        return JsonValue.Create(text);

    // Plain (unquoted) scalars: YAML 1.2 core schema.
    if (text is "" or "~" or "null" or "Null" or "NULL") return null;
    if (text is "true" or "True" or "TRUE") return JsonValue.Create(true);
    if (text is "false" or "False" or "FALSE") return JsonValue.Create(false);
    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[-+]?[0-9]+$")
        && long.TryParse(text, out var i)) return JsonValue.Create(i);
    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$")
        && double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var d))
        return JsonValue.Create(d);

    return JsonValue.Create(text);   // everything else is a string
}
