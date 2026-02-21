---
name: uloop-run-tests
description: "Execute Unity Test Runner and get detailed results. Use when: (1) running unit tests (EditMode/PlayMode), (2) verifying code changes, (3) diagnosing test failures — NUnit XML with error messages and stack traces is auto-saved when tests fail."
---

# uloop run-tests

Execute Unity Test Runner. When tests fail, NUnit XML results with error messages and stack traces are automatically saved. Read the XML file at `XmlPath` for detailed failure diagnosis.

## Usage

```bash
uloop run-tests [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--test-mode` | string | `EditMode` | Test mode: `EditMode`, `PlayMode` |
| `--filter-type` | string | `all` | Filter type: `all`, `exact`, `regex`, `assembly` |
| `--filter-value` | string | - | Filter value (test name, pattern, or assembly) |

## Global Options

| Option | Description |
|--------|-------------|
| `--project-path <path>` | Target a specific Unity project (mutually exclusive with `--port`). Path resolution follows the same rules as `cd` — absolute paths are used as-is, relative paths are resolved from cwd. |
| `-p, --port <port>` | Specify Unity TCP port directly (mutually exclusive with `--project-path`). |

## Examples

```bash
# Run all EditMode tests
uloop run-tests

# Run PlayMode tests
uloop run-tests --test-mode PlayMode

# Run specific test
uloop run-tests --filter-type exact --filter-value "MyTest.TestMethod"

# Run tests matching pattern
uloop run-tests --filter-type regex --filter-value ".*Integration.*"
```

## Output

Returns JSON with:
- `Success` (boolean): Whether all tests passed
- `Message` (string): Summary message
- `TestCount` (number): Total tests executed
- `PassedCount` (number): Passed tests
- `FailedCount` (number): Failed tests
- `SkippedCount` (number): Skipped tests
- `XmlPath` (string): Path to NUnit XML result file (auto-saved when tests fail)

### XML Result File

When tests fail, NUnit XML results are automatically saved to `{project_root}/.uloop/outputs/TestResults/<timestamp>.xml`. The XML contains per-test-case results including:
- Test name and full name
- Pass/fail/skip status and duration
- For failed tests: `<message>` (assertion error) and `<stack-trace>`
