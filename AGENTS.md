Comments in the code, commit messages, and PR titles and bodies should be written in English.

Always use `io.github.hatayama.uLoopMCP` as the namespace.

## Skill Description Guidelines

When writing or updating skill descriptions in `.claude/skills/*/SKILL.md`, follow the **"What → When → How"** structure:

1. **What** (first): What capability does the skill provide? (e.g., "Automate Unity Editor operations")
2. **When** (middle): When should the AI use it? Use the pattern "Use when you need to: (1) ..., (2) ..., (3) ..."
3. **How** (last): Technical implementation details (e.g., "Executes C# code dynamically via uloop CLI")

This structure follows the "inverted pyramid" principle - the most important information comes first, enabling both AI and users to quickly assess skill relevance for a given task.
