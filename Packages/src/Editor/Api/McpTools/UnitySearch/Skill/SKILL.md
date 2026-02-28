---
name: uloop-unity-search
description: "Search Unity project for assets. Use when: finding scenes, prefabs, scripts, materials, or other assets by name/type, or when user asks to search project files. Returns asset paths and metadata."
---

# uloop unity-search

Search Unity project using Unity Search.

## Usage

```bash
uloop unity-search [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--search-query` | string | - | Search query (supports Unity Search syntax) |
| `--providers` | array | - | Search providers (e.g., `asset`, `scene`, `menu`, `settings`, `packages`) |
| `--max-results` | integer | `50` | Maximum number of results |
| `--include-description` | boolean | `true` | Include detailed descriptions in results |
| `--include-metadata` | boolean | `false` | Include file metadata (size, modified date) |
| `--search-flags` | enum | `Default` | Search flags: `Default`, `Synchronous`, `WantsMore`, `Packages`, `Sorted` |
| `--save-to-file` | boolean | `false` | Save results to file |
| `--output-format` | enum | `JSON` | Output format when saving to file: `JSON`, `CSV`, `TSV` |
| `--auto-save-threshold` | integer | `100` | Auto-save to file when results exceed this count (0 to disable) |
| `--file-extensions` | array | - | Filter by file extension (e.g., `cs`, `prefab`, `mat`) |
| `--asset-types` | array | - | Filter by asset type (e.g., `Texture2D`, `GameObject`, `MonoScript`) |
| `--path-filter` | string | - | Filter by path pattern (supports wildcards) |

## Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`). Path resolution follows the same rules as `cd` — absolute paths are used as-is, relative paths are resolved from cwd. |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`). |

## Examples

```bash
# Search for assets
uloop unity-search --search-query "Player"

# Search with specific provider
uloop unity-search --search-query "t:Prefab" --providers asset

# Limit results
uloop unity-search --search-query "*.cs" --max-results 20
```

## Output

Returns JSON array of search results with paths and metadata.

## Notes

Use `uloop get-provider-details` to discover available search providers.
