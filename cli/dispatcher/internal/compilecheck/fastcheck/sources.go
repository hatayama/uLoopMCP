// Package fastcheck builds and runs the C# worker that checks only the changed files of an
// assembly against the rest of it.
package fastcheck

import "embed"

// workerSources holds the worker's C# sources. They ship as source, not as a built DLL, because
// the worker has to compile against the Roslyn of the Editor it runs with.
//
//go:embed worker/*.cs
var workerSources embed.FS

const workerSourceDirectory = "worker"
