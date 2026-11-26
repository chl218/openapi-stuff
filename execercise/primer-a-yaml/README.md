# Primer A — YAML crash course

Goal: read and write YAML confidently enough to hand-write an OpenAPI file in Lesson 1.

## Files
- `yamlcheck.pseudo`: a small C# console tool that parses YAML and prints it as JSON, so you can see what the parser understood.
- `broken.yaml`: a short OpenAPI file with six problems.
- `expected.json`: what `broken.yaml` should parse to once fixed.
- `convert-me.json`: JSON for you to rewrite as YAML.

## Run it
1. Build `YamlCheck` from `yamlcheck.pseudo` (or copy it from `solutions/primer-a-yaml/YamlCheck`).
2. `dotnet run --project YamlCheck -- broken.yaml`
3. Fix one problem, run again. Repeat until the output matches `expected.json`.
4. Write `convert-me.yaml` by hand from `convert-me.json`. Run YamlCheck on it and compare with the JSON.

## Done when
- YamlCheck prints `broken.yaml` with no error, and the output matches `expected.json`.
- `convert-me.yaml` uses no `{ }` or `[ ]`, uses a `|` block for `notes`, and parses to the same data as `convert-me.json`.
- You can explain why `version: 1.10` and `version: "1.10"` are different.

## Stretch
Put an anchor on the first region (`&norway`) and reuse it with an alias (`*norway`) somewhere else. What does YamlCheck print?
