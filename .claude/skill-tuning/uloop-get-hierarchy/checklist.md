# Checklist — uloop-get-hierarchy

## Scenario A (Entire scene with components)
- [critical] Issues `uloop get-hierarchy` (defaults give components)
- [critical] Recognizes that the actual hierarchy lives in the file at `hierarchyFilePath` (NOT inline) and reads that file before reporting
- [critical] Refers to documented response fields (`message`, `hierarchyFilePath`) by exact name; no invention

## Scenario B (Subtree without components)
- [critical] Issues `uloop get-hierarchy --root-path "Canvas/UI" --include-components false`
- Opens `hierarchyFilePath` to surface the subtree

## Scenario H (Selected, depth 1)
- [critical] Uses `--use-selection` and `--max-depth 1`; understands `--root-path` is ignored when `--use-selection` is true
- Opens the saved file to summarize
