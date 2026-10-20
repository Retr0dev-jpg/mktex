# Security

MkTeX is designed to typeset untrusted input, for example formulas from documents you did not write.

## Guarantees

- `MathEngine.Layout` and `MathEngine.Validate` do not throw for any input string. Errors are returned as `MathError`.
- Work is bounded:
  - sources longer than 10,000 characters are rejected;
  - group and environment nesting stops at 100 levels;
  - macro expansion stops after 1,000 expansions or 50,000 produced tokens, which blocks recursive and exponential macros;
  - stretchy assemblies stop at 200 repeated extenders.
- No file, network or process access. The only resource read is the embedded font. `\href` and `\url` draw their text but never open anything.
- The SVG renderer escapes the title and text and sanitizes the id prefix. Colors are normalized to `#RRGGBB` while parsing, so attribute values cannot carry markup.
- Nothing is logged. Exception messages and `MathError.Message` contain command names, but never the full source.

## Reporting a vulnerability

Please report vulnerabilities privately through [GitHub security advisories](https://github.com/Retr0dev-jpg/mktex/security/advisories/new) rather than in public issues. Include a minimal formula that reproduces the problem. You will receive a reply within a week.
