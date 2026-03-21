---
name: find-orphaned-meta
description: "Find and clean up orphaned .meta files in a Unity project. Use when you need to: (1) Find .meta files whose corresponding file or folder no longer exists, (2) Clean up Unity warnings about missing assets caused by stale .meta files, (3) Audit .meta hygiene after git operations like branch switches, merges, or file deletions. Also use proactively after deleting files or folders in a Unity project."
---

# Task

Find orphaned .meta files in the Unity project: $ARGUMENTS

## What

Detect `.meta` files in `Assets/` and `Packages/` whose corresponding file or folder no longer exists. Unity generates a `.meta` file for every asset — when the asset is deleted but the `.meta` remains, Unity logs warnings and the stale `.meta` pollutes the repository.

## When

Use when you need to:
1. Find `.meta` files left behind after file/folder deletions
2. Diagnose Unity warnings like "A meta data file (.meta) exists but its folder/asset can't be found"
3. Clean up after git operations (branch switch, merge, rebase) that may leave orphaned `.meta` files

## How

### Step 1: Run the detection script

```bash
.claude/skills/find-orphaned-meta/scripts/find-orphaned-meta.sh
```

The script scans `Assets/` and `Packages/` by default. Pass directory arguments to scan specific paths.

Output: one orphaned `.meta` path per line. Empty output means no orphans found.

### Step 2: Report findings

- If no orphans: report "No orphaned .meta files found."
- If orphans found: list them and ask the user whether to delete them.

### Step 3: Delete (with user confirmation)

After user approves, delete the orphaned `.meta` files:

```bash
rm -- "<orphaned-meta-path>"
```

Then run `uloop compile` to verify Unity no longer reports warnings about missing assets.
