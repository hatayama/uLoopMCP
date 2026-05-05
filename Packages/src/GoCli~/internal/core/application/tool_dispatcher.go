package application

import (
	"context"

	"github.com/hatayama/unity-cli-loop/Packages/src/GoCli/internal/core/ports"
	"github.com/hatayama/unity-cli-loop/Packages/src/GoCli/internal/shared/domain"
)

type ToolDispatchRequest struct {
	Command  string
	Params   map[string]any
	Progress func(string)
}

type ToolDispatcher struct {
	Bridge ports.UnityBridge
}

func (dispatcher ToolDispatcher) Dispatch(ctx context.Context, request ToolDispatchRequest) (domain.UnitySendOutcome, error) {
	return dispatcher.Bridge.SendWithProgressOutcome(ctx, request.Command, request.Params, request.Progress)
}
