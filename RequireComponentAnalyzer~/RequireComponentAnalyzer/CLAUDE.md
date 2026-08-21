# CLAUDE.md — RequireComponent Fetch Analyzer

## What this is

A Roslyn analyzer + code fix that closes the gap `[RequireComponent(typeof(T))]` leaves open:
Unity guarantees `T` exists on the GameObject, but does nothing to wire it to a field. This
analyzer flags an unwired field and offers a code fix that inserts the `GetComponent<T>()` fetch.
Unity consumes the committed `RequireComponentAnalyzer.dll` at the package root (`Editor/` or
package root — see Deployment below), whose `.meta` labels it as a `RoslynAnalyzer`; editing this
source project alone does not change Unity diagnostics.

## Project structure

```
RequireComponentAnalyzer/          Analyzer + code fix (netstandard2.0)
RequireComponentAnalyzer.Tests/    xUnit tests (net10.0)
```

Same layout and same `IsRoslynComponent`/`EnforceExtendedAnalyzerRules` csproj shape as
`Packages/Kodachi-ECS/Kodachi-ECS-Analyzer~` — copy that project's conventions when extending this one.

## Build & test

```powershell
& "C:\Users\felip\.dotnet\dotnet.exe" build RequireComponentAnalyzer\RequireComponentAnalyzer.csproj -c Release
& "C:\Users\felip\.dotnet\dotnet.exe" test RequireComponentAnalyzer.Tests\RequireComponentAnalyzer.Tests.csproj
```

## Diagnostic rule

| ID | Severity | Target | What it flags |
|----|----------|--------|----------------|
| URC001 | Info | MonoBehaviour field | A `public` or `[SerializeField]` private field whose type matches one of the class's `[RequireComponent(typeof(T))]` types, with no assignment anywhere in the class (no initializer, no `field = ...`, no `field = GetComponent<T>()`). |

**Scope is deliberately narrow.** Only public and `[SerializeField]` private fields are considered —
a plain private field could be legitimately unrelated to the `[RequireComponent]` contract, and this
rule stays silent rather than guess at intent. Info severity, not Warning/Error: this is a suggestion,
not a correctness bug — the field being unwired doesn't break anything until something reads it as null.

**Why the fetch is trusted to never be null:** Unity's `[RequireComponent]` guarantees the
referenced component type is present on the GameObject before the script's own `Awake` runs
(Unity adds it automatically, or refuses to add the script). So the generated fetch is a plain
`field = GetComponent<T>();` — no null-guard, matching this repo's fail-loud convention. If that
guarantee is ever violated (e.g. the component is removed by other code before this field reads
it), the resulting NullReferenceException is the correct signal, not something to swallow.

## Code fix

Offers three code actions (lightbulb choices), one per lifecycle method: `Fetch in Awake()`,
`Fetch in Start()`, `Fetch in Reset()`. Each inserts `field = GetComponent<T>();` into the
named method, creating the method if it doesn't already exist on the class. The generated
type reference is `Simplifier`-annotated so a fully-qualified name reduces to its short form
when the containing file already has the matching `using`.

`Reset()` is Unity's real editor-only lifecycle message (invoked when the component is first
added or via the inspector's Reset context command) — the fetch runs there like any other
lifecycle hook.

## Adding a new rule

Follow the Kodachi-ECS-Analyzer pattern: `const string` diagnostic ID + `DiagnosticDescriptor`
in `RequireComponentFetchAnalyzer.cs`, add to `SupportedDiagnostics`, register in `Initialize`,
add an `AnalyzerReleases.Unshipped.md` entry, write a test in
`RequireComponentFetchAnalyzerTests.cs`.

## Test patterns

Analyzer-only tests use `Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier`, with a
`UnityStub` preamble string providing minimal `UnityEngine` types (`MonoBehaviour`,
`RequireComponentAttribute`, `SerializeFieldAttribute`) so tests compile without referencing the
real Unity assemblies. The code-fix test drives the fix through a raw `AdhocWorkspace` (with all
loaded managed assemblies as references) rather than `CSharpCodeFixTest`, to sidestep that
harness's sensitivity to the saved-file line ending vs. the environment newline the `Formatter`
emits — a test-infrastructure quirk on Windows, not a behavioral difference in the generated code.

## Deployment to Unity

After building Release, copy
`RequireComponentAnalyzer/bin/Release/netstandard2.0/RequireComponentAnalyzer.dll` into the
`UnityEditorExtras` package (e.g. its root, alongside a `.meta` labelled `RoslynAnalyzer` — see
`Packages/Kodachi-ECS/Kodachi_ECS_Analyzer.dll.meta` for the exact `.meta` shape to copy) so Unity
picks it up as a Roslyn analyzer for scripts referencing `UnityEditorExtras`' assemblies.
