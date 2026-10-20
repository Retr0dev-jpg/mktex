# MkTeX

Native LaTeX math typesetting for .NET. MkTeX parses LaTeX math, lays it out with the rules of TeX's Appendix G driven by OpenType `MATH` metrics, and returns a platform-neutral list of drawing items. An SVG renderer is included. There is no JavaScript, no browser engine, and no native dependency.

- Targets `net8.0` and `net10.0` and uses only managed code.
- Bundles STIX Two Math (SIL Open Font License 1.1). It reads any OpenType font that has a `MATH` table and TrueType outlines.
- Fast: about 0.2 ms per formula on a desktop CPU (`dotnet run -c Release --project tools/MkTeX.Gallery -- --bench`).
- Robust against untrusted input. Sources are capped at 10,000 characters, nesting at 100 levels, macro expansion at 1,000 expansions, and expansion output at 50,000 tokens. Invalid input never throws: the result carries a `MathError` instead.

## Install

```powershell
dotnet add package MkTeX
```

## Usage

```csharp
using MkTeX;
using MkTeX.Rendering;

var layout = MathEngine.Layout(@"\int_0^\infty e^{-x^2}\,dx = \frac{\sqrt{\pi}}{2}",
    new MathOptions { FontSize = 20, Display = true });

if (layout.Error is { } error)
{
    Console.WriteLine($"{error.Code} at {error.Position}: {error.Message}");
}

string svg = SvgRenderer.Render(layout, new SvgOptions { IdPrefix = "eq1-", Title = "Gaussian integral" });
```

`MathLayout` holds pixel metrics: `Width`, `Height` (from the top to the baseline), and `Depth` (below the baseline). Its `Items` list uses coordinates that grow right and down:

| Item | Meaning |
| --- | --- |
| `GlyphItem` | A glyph id from `layout.Font`, its baseline origin, and its size in pixels per em. `layout.Font.Outline(id)` returns its path. |
| `RuleItem` | A filled rectangle (fraction bars, radicals, `\overline`, table rules). |
| `LineItem` | A stroke (`\cancel` and its variants). |
| `FrameItem` | A box with an optional fill and stroke (`\boxed`, `\colorbox`, `\fcolorbox`). |
| `TextItem` | Text that the math font cannot draw, for example CJK inside `\text{}`. Draw it with a system font. |

A color of `null` means the host's foreground color. Every other color is `#RRGGBB`. Use `layout.Tag` for `\tag{…}`; it is laid out separately so that you can align it to the right margin.

To draw natively (WPF, WinUI, Skia, and so on), convert each glyph outline once into a geometry, cache it by glyph id, and draw it with a translate and a uniform scale.

## Supported LaTeX

- **Structures:**
  - `\frac`, `\dfrac`, `\tfrac`, `\cfrac`, `\genfrac`, `\binom` (and its `d`/`t` forms), and the infix forms `\over`, `\atop`, `\choose`, `\brace`, `\brack`, `\above`;
  - `\sqrt[n]{}`;
  - scripts and primes;
  - `\limits`, `\nolimits`, `\displaylimits`.
- **Delimiters:**
  - `\left`, `\middle`, `\right`, with any delimiter the font can stretch;
  - `\big`, `\Big`, `\bigg`, `\Bigg`, with their `l`/`r`/`m` forms.
- **Accents:**
  - `\hat`, `\check`, `\tilde`, `\acute`, `\grave`, `\dot`, `\ddot`, `\dddot`, `\breve`, `\bar`, `\vec`, `\mathring`;
  - wide: `\widehat`, `\widetilde`, `\widecheck`, `\utilde`;
  - stretchy arrows: `\overrightarrow` and its siblings.
- **Over and under:**
  - `\overline`, `\underline`;
  - `\overbrace`/`\underbrace` (labelled through `^`/`_`), brackets, parens, groups;
  - `\overset`, `\underset`, `\overunderset`, `\stackrel`;
  - extensible arrows: `\xrightarrow[below]{above}`, `\xleftarrow`, `\xRightarrow`, `\xmapsto`, `\xhookrightarrow`, `\xrightleftharpoons`, `\xlongequal`, and the rest.
- **Environments:**
  - matrices: `matrix`, `pmatrix`, `bmatrix`, `Bmatrix`, `vmatrix`, `Vmatrix` (with `*[l|c|r]`), `smallmatrix`;
  - cases: `cases`, `dcases`, `rcases`, `drcases`;
  - arrays: `array` and `darray`, with `l c r | : p{}`; `subarray`;
  - `\hline`, `\hdashline`;
  - alignment and equations: `aligned`, `align(*)`, `alignat(*)`, `alignedat`, `split`, `gather(*)`, `gathered`, `multline(*)`, `equation(*)`, `eqnarray`;
  - `\substack`;
  - `\tag`, `\tag*`, `\notag`.
- **Symbols:**
  - Greek;
  - the amssymb relations, arrows, binary operators and miscellaneous symbols;
  - large operators (`\sum` through `\oiiint`);
  - named operators (`\sin` through `\argmax`), `\operatorname`, `\operatorname*`, `\DeclareMathOperator`;
  - `\not`, plus precomposed negations.
- **Fonts:**
  - `\mathrm`, `\mathit`, `\mathbf`, `\mathsf`, `\mathtt`, `\mathcal`, `\mathscr`, `\mathfrak`, `\mathbb`, `\mathsfit`;
  - `\boldsymbol`, `\bm`, `\pmb`;
  - old-style switches (`\rm`, `\bf`, and so on).
  - Letters map to the Unicode Mathematical Alphanumeric Symbols block.
- **Text:**
  - `\text`, `\textrm`, `\textbf`, `\textit`, `\textsf`, `\texttt`, `\emph`, `\mbox`;
  - inline `$…$`;
  - text accents (`\'e`, `\"o`, `\c{c}`, …) and text symbols.
- **Style and size:**
  - `\displaystyle` and its siblings;
  - `\tiny` through `\Huge`.
- **Color:**
  - `\color`, `\textcolor`, `\colorbox`, `\fcolorbox`;
  - xcolor and CSS names, `#hex`, and the `[HTML]`, `[rgb]` and `[RGB]` models.
- **Spacing:**
  - `\,`, `\:`, `\;`, `\!`, `\ `, `~`, `\quad`, `\qquad`, `\enspace`;
  - `\hspace`, `\kern`, `\mkern`, `\hskip`, `\mskip`, `\mspace`, in units em, ex, mu, pt, pc, in, bp, px, cm, mm, dd, cc, sp.
- **Boxes and layout:**
  - `\boxed`, `\fbox`;
  - `\phantom`, `\hphantom`, `\vphantom`, `\smash[tb]`;
  - `\llap`, `\rlap`, `\clap`, with their `\math…` forms;
  - `\raisebox`, `\rule`;
  - `\cancel`, `\bcancel`, `\xcancel`, `\sout`, `\cancelto`.
- **Macros:**
  - `\newcommand`, `\renewcommand`, `\providecommand` (with an optional first argument), `\def`, `\gdef`, `\let`;
  - user macros through `MathOptions.Macros`.
- **Extras:**
  - a practical subset of mhchem (`\ce{}`, `\pu{}`);
  - `\char"XXXX`;
  - `\TeX`, `\LaTeX`, `\KaTeX`;
  - braket helpers (`\bra`, `\ket`, `\braket`), `\pmod`, `\bmod`, `\R`, `\N`, `\Z`, `\Q`, `\C`.

## Not supported

- Text-mode paragraphs and line breaking, `\verb`, and `\href` links (only the link text is drawn).
- `\begin{CD}` commutative diagrams, `\sideset`, and `\mathchoice`.
- `multline` rows are centered rather than flushed left and right.
- OpenType kerning (`GPOS`), `ssty` script alternates, and CFF/OTF (PostScript) outlines.

## Development

```powershell
dotnet build MkTeX.slnx -warnaserror
dotnet test tests/MkTeX.Tests
dotnet run --project tools/MkTeX.Gallery -- gallery.html    # visual review of tools/MkTeX.Gallery/samples.txt
```

`tests/MkTeX.Tests/Corpus` holds more than 750 formulas (curated samples plus every symbol, operator and accent). Their SVG hashes are stored in `snapshots.tsv`. After an intentional layout change, review the gallery first, then update the snapshots:

```powershell
$env:MKTEX_UPDATE_SNAPSHOTS = "1"; dotnet test tests/MkTeX.Tests; Remove-Item Env:MKTEX_UPDATE_SNAPSHOTS
```

See [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md).

## License

MkTeX is licensed under the [GNU Lesser General Public License v3.0](LICENSE). You can use the package from applications under any license: when you ship MkTeX unmodified as a separate DLL, the LGPL obligations are met by keeping its notices and letting users replace the DLL. Modifications to MkTeX itself must be released under the LGPL.

The bundled STIX Two Math font is © The STIX Fonts Project authors and is licensed under the SIL Open Font License 1.1 ([THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)).
