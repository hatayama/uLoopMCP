package corecontract

import (
	"testing"

	"github.com/hatayama/unity-cli-loop/Packages/src/Cli/Shared/version"
)

func TestCoreContractProvidesRuntimeVersion(t *testing.T) {
	// Verifies that the core binary owns its runtime version outside shared packages.
	_, ok := version.Compare(Current.CoreVersion, Current.CoreVersion)
	if !ok {
		t.Fatalf("core version must be valid semver: %s", Current.CoreVersion)
	}
}

func TestCoreContractProvidesDispatcherRequirement(t *testing.T) {
	// Verifies that core owns the minimum dispatcher version needed to execute this binary.
	if Current.MinimumRequiredDispatcherVersion == "" {
		t.Fatal("minimum required dispatcher version must not be empty")
	}
	comparison, ok := version.Compare(Current.CoreVersion, Current.MinimumRequiredDispatcherVersion)
	if !ok {
		t.Fatalf("contract versions are not valid semver: %#v", Current)
	}
	if comparison < 0 {
		t.Fatalf("core version %s must not be lower than required dispatcher version %s", Current.CoreVersion, Current.MinimumRequiredDispatcherVersion)
	}
}

func TestCoreContractProvidesRequiredDispatcherVersionFlag(t *testing.T) {
	// Verifies that Unity and Go can share the flag used to query dispatcher compatibility.
	if Current.RequiredDispatcherVersionFlag != "--required-dispatcher-version" {
		t.Fatalf("required dispatcher version flag mismatch: %s", Current.RequiredDispatcherVersionFlag)
	}
}

func TestCoreContractProvidesDispatcherVersionEnvironment(t *testing.T) {
	// Verifies that core declares the environment key it accepts from the dispatcher.
	if Current.DispatcherVersionEnv != "ULOOP_DISPATCHER_VERSION" {
		t.Fatalf("dispatcher version env mismatch: %s", Current.DispatcherVersionEnv)
	}
}
