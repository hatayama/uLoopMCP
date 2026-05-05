package dispatchercontract

import "testing"

func TestDispatcherContractProvidesRuntimeVersion(t *testing.T) {
	// Verifies that the dispatcher binary owns its runtime version outside shared packages.
	if Current.DispatcherVersion != "3.0.0-beta.1" {
		t.Fatalf("dispatcher version mismatch: %s", Current.DispatcherVersion)
	}
}

func TestDispatcherContractProvidesVersionEnvironment(t *testing.T) {
	// Verifies that dispatcher owns the environment key it forwards to project-local core.
	if Current.DispatcherVersionEnv != "ULOOP_DISPATCHER_VERSION" {
		t.Fatalf("dispatcher version env mismatch: %s", Current.DispatcherVersionEnv)
	}
}
