package project

import (
	"path/filepath"
	"runtime"
	"strings"
	"testing"
)

func TestCreateEndpointUsesStableProjectHash(t *testing.T) {
	endpoint := CreateEndpoint("/tmp/MyProject")

	if runtime.GOOS == "windows" {
		if !strings.HasPrefix(endpoint.Address, `\\.\pipe\uloop-uLoopMCP-`) {
			t.Fatalf("unexpected windows pipe endpoint: %s", endpoint.Address)
		}
		return
	}

	expectedPrefix := filepath.Join("/tmp/uloop", "uLoopMCP-")
	if !strings.HasPrefix(endpoint.Address, expectedPrefix) {
		t.Fatalf("unexpected unix endpoint: %s", endpoint.Address)
	}
	if !strings.HasSuffix(endpoint.Address, ".sock") {
		t.Fatalf("unix endpoint should end with .sock: %s", endpoint.Address)
	}
}
