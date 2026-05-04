package dispatcher

import (
	"bufio"
	"context"
	"encoding/json"
	"fmt"
	"time"

	"github.com/hatayama/unity-cli-loop/Packages/src/GoCli/internal/adapters/framing"
	"github.com/hatayama/unity-cli-loop/Packages/src/GoCli/internal/adapters/project"
	"github.com/hatayama/unity-cli-loop/Packages/src/GoCli/internal/domain"
)

type launchReadyRequest struct {
	JSONRPC string                  `json:"jsonrpc"`
	Method  string                  `json:"method"`
	Params  map[string]any          `json:"params"`
	ID      int                     `json:"id"`
	Uloop   *domain.RequestMetadata `json:"x-uloop,omitempty"`
}

type launchReadyResponse struct {
	Result json.RawMessage      `json:"result,omitempty"`
	Error  *launchReadyRPCError `json:"error,omitempty"`
	ID     int                  `json:"id"`
}

type launchReadyRPCError struct {
	Message string `json:"message"`
}

func waitForLaunchReady(ctx context.Context, projectRoot string) error {
	timeoutContext, cancel := context.WithTimeout(ctx, launchReadinessTimeout)
	defer cancel()

	ticker := time.NewTicker(launchReadinessPoll)
	defer ticker.Stop()

	for {
		if err := probeLaunchReady(timeoutContext, projectRoot); err == nil {
			return nil
		}

		select {
		case <-timeoutContext.Done():
			if ctx.Err() != nil {
				return ctx.Err()
			}
			return fmt.Errorf("timed out waiting for Unity to become ready after launch")
		case <-ticker.C:
		}
	}
}

func probeLaunchReady(ctx context.Context, projectRoot string) error {
	probeContext, cancel := context.WithTimeout(ctx, launchProbeTimeout)
	defer cancel()

	connection, err := project.ResolveConnection(projectRoot, projectRoot)
	if err != nil {
		return err
	}

	conn, err := dialLaunchReadyEndpoint(probeContext, connection.Endpoint)
	if err != nil {
		return err
	}
	defer func() {
		_ = conn.Close()
	}()

	if deadline, ok := probeContext.Deadline(); ok {
		_ = conn.SetDeadline(deadline)
	}

	payload, err := json.Marshal(launchReadyRequest{
		JSONRPC: "2.0",
		Method:  "get-version",
		Params:  map[string]any{},
		ID:      1,
		Uloop:   connection.RequestMetadata,
	})
	if err != nil {
		return err
	}
	if err := framing.Write(conn, payload); err != nil {
		return err
	}

	responsePayload, err := framing.Read(bufio.NewReader(conn))
	if err != nil {
		return err
	}

	var response launchReadyResponse
	if err := json.Unmarshal(responsePayload, &response); err != nil {
		return err
	}
	if response.Error != nil {
		return fmt.Errorf("launch readiness probe failed: %s", response.Error.Message)
	}
	if len(response.Result) == 0 {
		return fmt.Errorf("launch readiness probe returned no result")
	}
	return nil
}
