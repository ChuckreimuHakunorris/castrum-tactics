# Contributing to the documentation

## Write and maintain docs

- Give public C# types and members meaningful XML documentation comments. Describe what callers need to know instead of repeating the type or member name.
- Use `<param>`, `<returns>`, and `<exception>` where applicable. Record important constraints, invariants, or side effects in the summary or `<remarks>`.
- Private implementation details and generated code generally need no API documentation.
- Update the [architecture overview](architecture.md) when a decision changes the overall design, and index accepted decisions in the [decision log](DecisionLog.md). Record milestone status in the [roadmap](DevelopmentRoadmap.md). Add links to follow-up work from historical studies while preserving their tested revisions, methods and results.
- Update the affected XML comments and articles in the same change as a public API or documented behavior.

## Build locally

Install the .NET 10 SDK and run these commands from the repository root:

```sh
dotnet tool restore
dotnet docfx metadata Documentation/docfx.json --warningsAsErrors
dotnet docfx build Documentation/docfx.json --warningsAsErrors
dotnet docfx serve Documentation/.docfx/site
```

Open [http://localhost:8080](http://localhost:8080) to preview the site. DocFX reads the application, Domain and AI C# projects and resolves their dependencies. Documentation builds require neither the Godot editor nor Android export tools. The tool manifest pins DocFX, and the API YAML and static HTML are generated locally or in CI.

DocFX can leave old generated files after a type is renamed or removed. To rebuild from scratch, preview the cleanup with `git clean -ndX -- Documentation/api/ Documentation/.docfx/`. Run the same command with `-fdX` to remove the listed ignored files, then rebuild.

## Publish to GitHub Pages

In the repository, open **Settings > Pages**. Under **Build and deployment**, set **Source** to **GitHub Actions**. This is a one-time repository setting. [GitHub Pages publishing source](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site)

Once the workflow is on `main`, pushes to `main` build and publish the docs. To run it manually, open **Actions > Publish documentation > Run workflow** and select `main`. The deployment job shows the site URL after a successful run.
