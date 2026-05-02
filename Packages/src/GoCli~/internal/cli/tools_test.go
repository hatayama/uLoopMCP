package cli

import (
	"os"
	"path/filepath"
	"testing"
)

func TestBuildToolParamsConvertsSchemaTypes(t *testing.T) {
	tool := toolDefinition{
		Name: "sample-tool",
		InputSchema: inputSchema{
			Properties: map[string]toolProperty{
				"Enabled": {Type: "boolean"},
				"Count":   {Type: "integer"},
				"Names":   {Type: "array"},
			},
		},
	}

	params, projectPath, err := buildToolParams(
		[]string{
			"--enabled", "true",
			"--count", "12",
			"--names", "a,b",
			"--project-path", "/tmp/project",
		},
		tool,
	)
	if err != nil {
		t.Fatalf("buildToolParams failed: %v", err)
	}

	if projectPath != "/tmp/project" {
		t.Fatalf("project path mismatch: %s", projectPath)
	}
	if params["Enabled"] != true {
		t.Fatalf("Enabled mismatch: %#v", params["Enabled"])
	}
	if params["Count"] != 12 {
		t.Fatalf("Count mismatch: %#v", params["Count"])
	}
	names, ok := params["Names"].([]string)
	if !ok || len(names) != 2 || names[0] != "a" || names[1] != "b" {
		t.Fatalf("Names mismatch: %#v", params["Names"])
	}
}

func TestLoadToolsFiltersInternalSkillToolsFromCache(t *testing.T) {
	projectRoot := t.TempDir()
	writeTestSkill(t, projectRoot, "Assets/Editor/InternalTool/Skill", `---
name: uloop-internal-tool
internal: true
---

# internal
`)
	writeToolCache(t, projectRoot, `{
  "version": "test",
  "tools": [
    {
      "name": "internal-tool",
      "description": "internal",
      "inputSchema": {"type": "object", "properties": {}}
    },
    {
      "name": "public-tool",
      "description": "public",
      "inputSchema": {"type": "object", "properties": {}}
    }
  ]
}`)

	cache, err := loadTools(projectRoot)
	if err != nil {
		t.Fatalf("loadTools failed: %v", err)
	}

	if _, ok := findTool(cache, "internal-tool"); ok {
		t.Fatalf("internal tool was not filtered: %#v", cache.Tools)
	}
	if _, ok := findTool(cache, "public-tool"); !ok {
		t.Fatalf("public tool was filtered: %#v", cache.Tools)
	}
}

func TestLoadDefaultToolsDoesNotExposeInternalSkillTools(t *testing.T) {
	cache := loadDefaultTools()

	for _, toolName := range []string{"get-project-info", "get-version"} {
		if _, ok := findTool(cache, toolName); ok {
			t.Fatalf("internal tool %s was exposed by default tools", toolName)
		}
	}
}

func TestBuildToolParamsAcceptsNegativeNumericValues(t *testing.T) {
	tool := toolDefinition{
		Name: "sample-tool",
		InputSchema: inputSchema{
			Properties: map[string]toolProperty{
				"DeltaX": {Type: "number"},
				"Count":  {Type: "integer"},
			},
		},
	}

	params, _, err := buildToolParams(
		[]string{
			"--delta-x", "-10.5",
			"--count", "-2",
		},
		tool,
	)
	if err != nil {
		t.Fatalf("buildToolParams failed: %v", err)
	}

	if params["DeltaX"] != -10.5 {
		t.Fatalf("DeltaX mismatch: %#v", params["DeltaX"])
	}
	if params["Count"] != -2 {
		t.Fatalf("Count mismatch: %#v", params["Count"])
	}
}

func TestParseGlobalProjectPathAcceptsLeadingOption(t *testing.T) {
	remaining, projectPath, err := parseGlobalProjectPath(
		[]string{
			"--project-path", "/tmp/project",
			"compile",
			"--force-recompile", "true",
		},
	)
	if err != nil {
		t.Fatalf("parseGlobalProjectPath failed: %v", err)
	}

	if projectPath != "/tmp/project" {
		t.Fatalf("project path mismatch: %s", projectPath)
	}
	expected := []string{"compile", "--force-recompile", "true"}
	if len(remaining) != len(expected) {
		t.Fatalf("remaining length mismatch: %#v", remaining)
	}
	for index, value := range expected {
		if remaining[index] != value {
			t.Fatalf("remaining mismatch: %#v", remaining)
		}
	}
}

func writeToolCache(t *testing.T, projectRoot string, content string) {
	t.Helper()
	cachePath := filepath.Join(projectRoot, cacheDirectoryName, cacheFileName)
	if err := os.MkdirAll(filepath.Dir(cachePath), 0o755); err != nil {
		t.Fatalf("failed to create tool cache directory: %v", err)
	}
	if err := os.WriteFile(cachePath, []byte(content), 0o644); err != nil {
		t.Fatalf("failed to write tool cache: %v", err)
	}
}
