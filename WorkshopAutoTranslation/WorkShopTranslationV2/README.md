# WorkShopTranslationV2 Tool

This program translates workshop markdown files with [GitHub Models](https://github.com/marketplace/models). It still supports the existing file/folder translation flow, and now also supports scanning the `NuevoFoundation/workshops` repository for missing language files and preparing one PR per target language.

## Features

1. **Reading from file/folder**: Reads markdown files from a file path or directory tree.
2. **Translation using GitHub Models**: Uses the Azure AI Inference SDK to translate workshop content.
3. **Saving translated content**: Writes translated markdown into the matching target language folder.
4. **Gap scanning**: Finds missing translated files by comparing `content/english/<workshop>` against sibling language folders.
5. **PR automation**: Creates or updates a deterministic branch/PR per target language with a configurable file cap per run.

## Prerequisites

- A GitHub token in `GITHUB_TOKEN`
- A .NET SDK installed locally
- A local clone of `https://github.com/NuevoFoundation/workshops` when using gap scanning / PR automation
- `git` and `gh` CLI installed and authenticated for PR automation

## Set environment variables

If you're using bash:

`export GITHUB_TOKEN="<your-github-token-goes-here>"`

If you're in PowerShell:

`$Env:GITHUB_TOKEN="<your-github-token-goes-here>"`

If you're using Windows command prompt:

`set GITHUB_TOKEN=<your-github-token-goes-here>`

## Usage

### Legacy translation flow

Navigate to the `WorkShopTranslationV2` directory and run:

`dotnet run <inputPath> <targetLanguage> <model (optional)>`

Example:

`dotnet run C:\Documents\workshops\content\english\csharp-basics french gpt-4o`

### Scan gaps only

`dotnet run -- --scan-gaps <path-to-local-clone-of-workshops-repo> --report`

Optional JSON output:

`dotnet run -- --scan-gaps <path-to-local-clone-of-workshops-repo> --report --format json`

Optional language filter:

`dotnet run -- --scan-gaps <path-to-local-clone-of-workshops-repo> --report --language french`

### Create or update language PRs

`dotnet run -- --scan-gaps <path-to-local-clone-of-workshops-repo> --create-prs [--model gpt-4o] [--language french] [--max-workshops-per-pr 8] [--base-branch master] [--branch-prefix auto-translate/] [--repo owner/name] [--dry-run]`

Behavior:

- Uses `content/english/<workshop>` as the source of truth
- Detects missing translated markdown files per target language
- Warns about orphan non-English workshops with no English counterpart
- Reuses an open PR when the target branch already has one
- Limits each language PR to `--max-workshops-per-pr` whole workshops per run (default 8) — workshops are never split across runs; a run always completes entirely, or defers entirely, per workshop
- `--dry-run` reports what would happen without translating files, pushing branches, or creating/updating PRs

## Notes

- Supported languages come from the shared `LanguageCatalog` dictionary used by both legacy translation and gap scanning.
- Keep `GITHUB_TOKEN` confidential and prefer short-lived credentials.
