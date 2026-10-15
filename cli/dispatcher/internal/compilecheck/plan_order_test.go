package compilecheck

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

// Verifies that when two assemblies each fail their own check, planning reports the one first in
// name order, whichever check finishes first.
func TestBuildCompilePlanReportsTheFirstFailingAssemblyInNameOrder(t *testing.T) {
	projectRoot := t.TempDir()
	names := []string{}
	for index := range 20 {
		names = append(names, "A"+string(rune('A'+index)))
	}
	for _, name := range names {
		writePlanAssembly(t, projectRoot, name, nil)
	}
	settlePlanProject(t, projectRoot, names...)
	for _, name := range []string{names[2], names[17]} {
		if err := os.Remove(filepath.Join(
			projectRoot, planDagDirectory, name+".UnityAdditionalFile.txt")); err != nil {
			t.Fatalf("failed to remove the additional file of %s: %v", name, err)
		}
	}

	_, err := BuildCompilePlan(projectRoot, planDagDirectory, false)

	if err == nil || !strings.Contains(err.Error(), "the Bee artifacts are incomplete for "+names[2]+";") {
		t.Fatalf("err = %v, want the incomplete artifacts of %s reported", err, names[2])
	}
}
