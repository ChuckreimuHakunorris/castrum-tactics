using System.Diagnostics;
using HtmlAgilityPack;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;

if (args is ["--self-test"])
{
    SelfTest();
    return 0;
}

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage from the repository root: DocumentationLinks <generated-site> or --self-test");
    return 2;
}

var gitInfo = new ProcessStartInfo("git") { RedirectStandardOutput = true };
foreach (string argument in new[] { "ls-files", "-z" })
{
    gitInfo.ArgumentList.Add(argument);
}
using var git = Process.Start(gitInfo) ?? throw new InvalidOperationException("Git could not start.");
string[] trackedFiles = git.StandardOutput.ReadToEnd().Split('\0', StringSplitOptions.RemoveEmptyEntries);
git.WaitForExit();
if (git.ExitCode != 0) return git.ExitCode;

var repository = new LinkCheck(Environment.CurrentDirectory, trackedFiles, markdown: true);
var documents = trackedFiles.Where(path => Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase)).ToArray();
var sitePath = Path.GetFullPath(args[0]);
if (!File.Exists(Path.Combine(sitePath, "index.html")))
{
    Console.Error.WriteLine($"Build the DocFX site first. Missing {Path.Combine(sitePath, "index.html")}.");
    return 2;
}

var siteFiles = Directory.EnumerateFiles(sitePath, "*", SearchOption.AllDirectories)
    .Select(path => Path.GetRelativePath(sitePath, path).Replace(Path.DirectorySeparatorChar, '/')).ToArray();
var pages = siteFiles.Where(path => Path.GetExtension(path).Equals(".html", StringComparison.OrdinalIgnoreCase)).ToArray();
var failures = repository.Validate(documents).Concat(new LinkCheck(sitePath, siteFiles, markdown: false).Validate(pages)).ToArray();
foreach (string failure in failures)
{
    Console.Error.WriteLine(failure);
}
Console.WriteLine($"Checked {documents.Length} repository Markdown files and {pages.Length} generated HTML pages. {failures.Length} broken local links.");
return failures.Length == 0 ? 0 : 1;

static void SelfTest()
{
    string root = Directory.CreateTempSubdirectory("castrum-links-").FullName;
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        File.WriteAllText(Path.Combine(root, "README.md"), """
            [Encoded target](nested/guide%20page.md#café--api)
            [Duplicate heading](nested/guide%20page.md#café--api-1)
            [External](https://example.com/absent#opaque)
            [xrefs are checked by DocFX](xref:Example.Type)

            ```md
            [Example](missing.md#not-a-real-link)
            ```

            [Wrong case](nested/Guide%20page.md)
            [Missing fragment](nested/guide%20page.md#absent)
            """);
        File.WriteAllText(Path.Combine(root, "nested", "guide page.md"), "# Café & API\n\n# Café & API\n");
        var markdown = new LinkCheck(root, ["README.md", "nested/guide page.md"], markdown: true);
        string[] markdownErrors = markdown.Validate(["README.md"]).ToArray();
        if (markdownErrors is not [var wrongCase, var missingId] ||
            !wrongCase.Contains("missing target", StringComparison.Ordinal) ||
            !missingId.Contains("missing fragment", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, markdownErrors));
        }

        File.WriteAllText(Path.Combine(root, "index.html"), """
            <a href="nested/page.html#Api%2EMember">Good</a>
            <a href="nested/page.html#Bad">Bad</a>
            <img src="nested/missing.png" alt="">
            """);
        File.WriteAllText(Path.Combine(root, "nested", "page.html"), """<h1 id="Api.Member">API</h1>""");
        var html = new LinkCheck(root, ["index.html", "nested/page.html"], markdown: false);
        string[] htmlErrors = html.Validate(["index.html"]).ToArray();
        if (htmlErrors is not [var badId, var missingImage] ||
            !badId.Contains("missing fragment", StringComparison.Ordinal) ||
            !missingImage.Contains("missing target", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, htmlErrors));
        }
        Console.WriteLine("Link self-test passed for Markdown and rendered HTML.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

internal sealed class LinkCheck(string root, IEnumerable<string> files, bool markdown)
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables().UseAutoLinks().UseAutoIdentifiers(AutoIdentifierOptions.GitHub).Build();

    private readonly HashSet<string> _files = new(files, StringComparer.Ordinal);
    private readonly Dictionary<string, HtmlDocument> _documents = new(StringComparer.Ordinal);

    internal IEnumerable<string> Validate(IEnumerable<string> sources)
    {
        string[] directories = _files.SelectMany(path => path.Select((character, index) => (character, index))
                .Where(item => item.character == '/').Select(item => path[..item.index]))
            .Distinct(StringComparer.Ordinal).ToArray();
        foreach (string source in sources)
        {
            var links = Read(source).DocumentNode.Descendants()
                .SelectMany(node => node.Attributes)
                .Where(attribute => attribute.Name is "href" or "src" or "poster");
            foreach (var link in links)
            {
                string url = (HtmlEntity.DeEntitize(link.Value) ?? "").Trim();
                if (url.Length == 0 || url.StartsWith("//", StringComparison.Ordinal) ||
                    (!url.StartsWith('/') && Uri.TryCreate(url, UriKind.Absolute, out _))) continue;

                int hash = url.IndexOf('#');
                string fragment = hash < 0 ? "" : Uri.UnescapeDataString(url[(hash + 1)..]);
                string relative = Uri.UnescapeDataString((hash < 0 ? url : url[..hash]).Split('?', 2)[0]);
                string destination = relative.Length == 0 ? source : Resolve(source, relative);
                if (!markdown && (relative.EndsWith('/') || directories.Contains(destination, StringComparer.Ordinal)))
                    destination = destination.Length == 0 ? "index.html" : destination + "/index.html";

                string? reason = !_files.Contains(destination) &&
                    !(markdown && (destination.Length == 0 || directories.Contains(destination, StringComparer.Ordinal)))
                    ? "missing target or incorrect path case"
                    : fragment.Length != 0 && !fragment.StartsWith(":~:text=", StringComparison.Ordinal) &&
                      IsDocument(destination) && !HasAnchor(Read(destination), fragment)
                        ? "missing fragment or incorrect identifier case"
                        : null;
                if (reason is not null)
                {
                    string location = markdown ? source : $"{source}:{link.Line}";
                    yield return $"{location} -> {url}: {reason}";
                }
            }
        }
    }

    private bool IsDocument(string path) => markdown
        ? path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        : path.EndsWith(".html", StringComparison.OrdinalIgnoreCase);

    private static string Resolve(string source, string target)
    {
        string parent = source[..(source.LastIndexOf('/') + 1)];
        var parts = new List<string>();
        foreach (string part in (target.StartsWith('/') ? target : parent + target).Split('/'))
        {
            if (part == "..")
            {
                if (parts.Count == 0) return "../" + target;
                parts.RemoveAt(parts.Count - 1);
            }
            else if (part is not ("" or ".")) parts.Add(part);
        }
        return string.Join('/', parts);
    }

    private HtmlDocument Read(string path)
    {
        if (_documents.TryGetValue(path, out var document)) return document;

        string content = File.ReadAllText(Path.Combine(root, path));
        document = new HtmlDocument();
        document.LoadHtml(markdown ? Markdown.ToHtml(content, Pipeline) : content);
        _documents.Add(path, document);
        return document;
    }

    private static bool HasAnchor(HtmlDocument document, string fragment) => document.DocumentNode.Descendants()
        .Any(node => node.GetAttributeValue("id", "") == fragment ||
                     (node.Name == "a" && node.GetAttributeValue("name", "") == fragment));
}
