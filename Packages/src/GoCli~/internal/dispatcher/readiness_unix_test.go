//go:build !windows

package dispatcher

import (
	"bufio"
	"context"
	"encoding/json"
	"net"
	"os"
	"path/filepath"
	"testing"
	"time"

	"github.com/hatayama/unity-cli-loop/Packages/src/GoCli/internal/adapters/framing"
	"github.com/hatayama/unity-cli-loop/Packages/src/GoCli/internal/adapters/project"
)

func TestWaitForLaunchReadyProbesUnityServer(t *testing.T) {
	// Verifies that bootstrap launch waits for a real Unity RPC response before returning.
	projectRoot := t.TempDir()
	createUnityProject(t, projectRoot)
	connection, err := project.ResolveConnection(projectRoot, projectRoot)
	if err != nil {
		t.Fatalf("ResolveConnection failed: %v", err)
	}
	if err := os.MkdirAll(filepath.Dir(connection.Endpoint.Address), 0o755); err != nil {
		t.Fatalf("failed to create endpoint directory: %v", err)
	}
	listener, err := net.Listen(connection.Endpoint.Network, connection.Endpoint.Address)
	if err != nil {
		t.Fatalf("failed to listen on endpoint: %v", err)
	}
	defer func() {
		_ = listener.Close()
		_ = os.Remove(connection.Endpoint.Address)
	}()
	served := make(chan error, 1)
	go serveLaunchReadyProbe(listener, served)

	if err := waitForLaunchReady(context.Background(), projectRoot); err != nil {
		t.Fatalf("waitForLaunchReady failed: %v", err)
	}

	select {
	case err := <-served:
		if err != nil {
			t.Fatalf("probe server failed: %v", err)
		}
	case <-time.After(time.Second):
		t.Fatal("timed out waiting for probe server")
	}
}

func serveLaunchReadyProbe(listener net.Listener, served chan<- error) {
	conn, err := listener.Accept()
	if err != nil {
		served <- err
		return
	}
	defer func() {
		_ = conn.Close()
	}()

	_, err = framing.Read(bufio.NewReader(conn))
	if err != nil {
		served <- err
		return
	}

	response := map[string]any{
		"jsonrpc": "2.0",
		"result":  map[string]any{"version": "test"},
		"id":      1,
	}
	payload, err := json.Marshal(response)
	if err != nil {
		served <- err
		return
	}
	served <- framing.Write(conn, payload)
}
