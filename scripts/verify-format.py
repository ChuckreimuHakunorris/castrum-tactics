"""Check whitespace on an isolated copy of the tracked, active C# sources."""
import argparse
from pathlib import Path, PurePosixPath
import shutil
import subprocess
from tempfile import TemporaryDirectory

SOURCE_AREAS = {"AI", "Benchmarks", "Domain", "Match", "Scenes", "Tests", "scripts"}
FROZEN = {"Frozen", "Frozen8C", "Frozen8D2", "Frozen8D3", "Frozen8D4", "Frozen8E"}


def active_source(path):
    parts = PurePosixPath(path).parts
    return (
        len(parts) >= 2 and path.endswith(".cs") and parts[0] in SOURCE_AREAS
        and not (parts[0] == "Benchmarks" and parts[1] in FROZEN)
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", default="HEAD", help="Check added or changed files relative to this Git revision.")
    args = parser.parse_args()
    root = Path(subprocess.check_output(["git", "rev-parse", "--show-toplevel"], text=True).strip())
    inventory = subprocess.check_output(["git", "ls-files", "-z", "--", "*.cs"], cwd=root)
    changes = subprocess.check_output(
        ["git", "diff", "--name-only", "-z", "--diff-filter=ACMRT", args.base, "--", "*.cs"], cwd=root
    )
    selected = set(changes.decode("utf-8").split("\0"))
    sources = [
        path for path in inventory.decode("utf-8").split("\0") if path in selected and active_source(path)
    ]
    if not sources:
        print("No added or changed first-party C# files to format.")
        return 0
    with TemporaryDirectory(prefix="castrum-format-") as directory:
        workspace = Path(directory)
        shutil.copyfile(root / ".editorconfig", workspace / ".editorconfig")
        for path in sources:
            file = workspace / path
            file.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(root / path, file)

        print(f"Checking whitespace in {len(sources)} first-party C# files.", flush=True)
        result = subprocess.run(
            ["dotnet", "format", "whitespace", str(workspace), "--folder", "--verify-no-changes"],
            cwd=root,
            check=False,
        )
        return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
