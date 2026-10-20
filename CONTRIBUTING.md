# Contributing

Thank you for helping. Documentation, code and comments are in English.

## Layout

| Path | Contents |
| --- | --- |
| `src/MkTeX/Fonts` | OpenType reader (`cmap`, `hmtx`, `glyf`/`loca`, `MATH`), with outlines and metrics in em. |
| `src/MkTeX/Parsing` | Lexer, macro expansion (`TokenStream`), parser (`Parser*.cs`), symbol tables, alphabets, colors, mhchem subset. |
| `src/MkTeX/Layout` | Box model and `Typesetter*.cs`: spacing, scripts, fractions, radicals, delimiters, decorations, arrays. |
| `src/MkTeX/Rendering` | SVG renderer. |
| `tests/MkTeX.Tests` | xUnit rule tests, parser tests, corpus snapshots, fuzzing. |
| `tools/MkTeX.Gallery` | HTML gallery (`samples.txt`), font probe (`--probe 222B`), benchmark (`--bench`). |

## Rules

- `dotnet build MkTeX.slnx -warnaserror` must pass with zero warnings, and `dotnet test tests/MkTeX.Tests` must pass.
- `.cs`, `.csproj`, `.props`, `.slnx` and `.ps1` files are UTF-8 with BOM (see `.editorconfig`). Markdown, YAML and TSV files have no BOM.
- Layout changes: render the gallery, compare it before and after, then update `Corpus/snapshots.tsv` with `MKTEX_UPDATE_SNAPSHOTS=1`. Explain the visual change in the pull request.
- New commands: add them to the parser or the symbol tables, add a line to `Corpus/formulas.txt`, and add a rule test when the layout is not trivial. Update the "Supported LaTeX" section of the README.
- Keep the limits in `SECURITY.md` true. Every loop that depends on input must be bounded.
- Write comments only for constraints that the code does not make obvious.

## Releases

Bump `VersionPrefix` in `Directory.Build.props`, update `CHANGELOG.md`, then push a tag `vX.Y.Z`. The release workflow builds, tests and packs, creates a GitHub Release with the `.nupkg`, and publishes to nuget.org when the `NUGET_API_KEY` repository secret is set.
