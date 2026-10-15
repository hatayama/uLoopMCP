package compilecheck

import "sync"

// planInputs is everything planning reads from the disk before it decides anything.
type planInputs struct {
	graph    assemblyGraph
	graphErr error
	tree     projectTree
	// failedLastBuild names the assemblies the last Unity build failed to compile. It is empty for
	// an --all run.
	failedLastBuild map[string]bool
}

// loadPlanInputs reads the response files, walks the project and reads the Bee log at the same time,
// and returns once all three are done.
// Why at the same time: none of the three reads what another produces, and on a large project the
// response files and the Bee log take about as long together as the walk does alone.
// Why it waits for all three even when the response files fail: no read may still be running when
// planning returns, and waiting here rather than at each error exit keeps that true by construction.
func loadPlanInputs(projectRoot string, dagDir string, all bool) planInputs {
	inputs := planInputs{failedLastBuild: map[string]bool{}}
	runConcurrently(
		func() { inputs.graph, inputs.graphErr = loadAssemblyGraph(projectRoot, dagDir) },
		func() { inputs.tree = walkProjectTree(projectRoot) },
		func() {
			// Why the Bee log is read once per run: it describes the whole of the last build, and
			// reading it per assembly would reread tens of megabytes for every response file. An
			// --all run reads none, because it selects every assembly whatever the last build made
			// of it.
			if !all {
				inputs.failedLastBuild = ReadFailedUnityCompiles(projectRoot, dagDir)
			}
		},
	)

	return inputs
}

// runConcurrently runs every task at the same time and returns once all of them have finished. Each
// task must write only to what no other task reads or writes.
func runConcurrently(tasks ...func()) {
	var running sync.WaitGroup
	for _, task := range tasks {
		running.Go(task)
	}
	running.Wait()
}
