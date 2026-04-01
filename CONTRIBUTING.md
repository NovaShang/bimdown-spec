# Contributing to BimDown

First off, thank you for considering contributing to BimDown! The open-source community around building data is small but growing, and your input is highly valued.

## Development Setup

The project is split into two main parts: the Node.js CLI tool, and the C# Revit Add-in.

### CLI Development (Node.js)

The CLI tool acts as a parser, validator, and DuckDB SQL query engine for the BimDown format.

1. Ensure you have Node.js 22+ installed.
2. Navigate to the `cli/` directory.
3. Install dependencies:
   ```bash
   npm install
   ```
4. Build the project:
   ```bash
   npm run build
   ```
5. Run the tests:
   ```bash
   npm test
   ```

### Revit Add-in Development (C#)

The Revit Add-in allows bidirectional sync between Revit 2026+ and the BimDown format.

1. Open the project folder in Visual Studio 2022 or Rider.
2. The project uses `.NET 8.0`. Ensure you have the `.NET 8.0 SDK` installed.
3. The project references the `Nice3point.Revit.Api` NuGet packages. You do not need Revit installed locally just to build the add-in.
4. To run tests (TUnit), you will need Revit 2026 installed locally, as it executes integration tests against an active Revit process.

## Pull Request Process

1. Ensure any changes to the CLI pass linting and testing (`npm test`).
2. Update the README or specification (`spec/readme.md`) with details of any schema changes to the BimDown format.
3. Merge requests should use [Conventional Commits](https://www.conventionalcommits.org/) syntax (e.g., `feat: add support for foundation`, `fix: duckdb geometry join`).

Thank you for contributing!
