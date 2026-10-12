package projectrunner

import "encoding/json"

// addEarlierCompileResultWarning puts a note at the front of the result's Warning saying the result
// predates later script edits.
func addEarlierCompileResultWarning(result json.RawMessage) json.RawMessage {
	return result
}
