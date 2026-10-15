package compilecheck

import (
	"encoding/json"
	"testing"
)

// Verifies the JSON form of a diagnostic, field names and order included. The result store records
// diagnostics exactly as it is handed them, so changing this form would change how the records an
// earlier run left are read, without anything marking those records as written by another version.
func TestDiagnosticKeepsTheJSONFormTheResultStoreRecords(t *testing.T) {
	diagnostic := Diagnostic{
		Severity: severityError,
		Code:     "CS0103",
		Message:  "The name 'Missing' does not exist in the current context",
		File:     "Assets/Alpha/Alpha.cs",
		Line:     3,
		Column:   9,
	}

	content, err := json.Marshal(diagnostic)
	if err != nil {
		t.Fatalf("failed to encode the diagnostic: %v", err)
	}

	want := `{"Severity":"error","Code":"CS0103","Message":"The name 'Missing' does not exist in the current context",` +
		`"File":"Assets/Alpha/Alpha.cs","Line":3,"Column":9}`
	if string(content) != want {
		t.Errorf("JSON form =\n%s\nwant\n%s", content, want)
	}
}
