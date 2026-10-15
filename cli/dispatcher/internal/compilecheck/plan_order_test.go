package compilecheck

import (
	"errors"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

// Verifies that when two assemblies each fail their own check, planning reports the one first in
// name order.
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

// Verifies the first failing assembly in name order is reported even when a later assembly's check
// finishes first: the earlier check is held until the later one has returned its error.
func TestDecideAssembliesInNameOrderReportsTheFirstErrorWhateverFinishesFirst(t *testing.T) {
	graph := assemblyGraph{names: []string{"A", "B", "C"}, sources: map[string][]string{}}
	laterReturned := make(chan struct{})
	errB := errors.New("B failed")

	_, err := decideAssembliesInNameOrder(graph, map[string]bool{}, false, func(name string) assemblyCheck {
		switch name {
		case "B":
			<-laterReturned

			return assemblyCheck{err: errB}
		case "C":
			defer close(laterReturned)

			return assemblyCheck{err: errors.New("C failed")}
		default:
			return assemblyCheck{}
		}
	})

	if !errors.Is(err, errB) {
		t.Fatalf("err = %v, want the error of B", err)
	}
}
