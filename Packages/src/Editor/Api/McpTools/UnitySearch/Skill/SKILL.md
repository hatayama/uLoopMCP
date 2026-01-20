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
| `--search-query` | string | - | Search query |
| `--providers` | array | - | Search providers (e.g., `asset`, `scene`, `find`) |
| `--max-results` | integer | `50` | Maximum number of results |
| `--save-to-file` | boolean | `false` | Save results to file |

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
