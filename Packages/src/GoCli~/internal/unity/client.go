package unity

import (
	"bufio"
	"context"
	"encoding/json"
	"fmt"
	"time"

	"github.com/hatayama/unity-cli-loop/Packages/src/GoCli/internal/framing"
	"github.com/hatayama/unity-cli-loop/Packages/src/GoCli/internal/project"
)

const requestTimeout = 180 * time.Second

type Client struct {
	connection project.Connection
	requestID  int
}

type rpcRequest struct {
	JSONRPC string                   `json:"jsonrpc"`
	Method  string                   `json:"method"`
	Params  map[string]any           `json:"params"`
	ID      int                      `json:"id"`
	Uloop   *project.RequestMetadata `json:"x-uloop,omitempty"`
}

type rpcResponse struct {
	JSONRPC string          `json:"jsonrpc"`
	Result  json.RawMessage `json:"result,omitempty"`
	Error   *rpcError       `json:"error,omitempty"`
	ID      int             `json:"id"`
}

type rpcError struct {
	Code    int             `json:"code"`
	Message string          `json:"message"`
	Data    json.RawMessage `json:"data,omitempty"`
}

func NewClient(connection project.Connection) *Client {
	return &Client{connection: connection}
}

func (client *Client) Send(ctx context.Context, method string, params map[string]any) (json.RawMessage, error) {
	ctx, cancel := context.WithTimeout(ctx, requestTimeout)
	defer cancel()

	conn, err := dialEndpoint(ctx, client.connection.Endpoint)
	if err != nil {
		return nil, fmt.Errorf("connection error: %w", err)
	}
	defer conn.Close()

	client.requestID++
	request := rpcRequest{
		JSONRPC: "2.0",
		Method:  method,
		Params:  params,
		ID:      client.requestID,
		Uloop:   client.connection.RequestMetadata,
	}

	payload, err := json.Marshal(request)
	if err != nil {
		return nil, err
	}

	if deadline, ok := ctx.Deadline(); ok {
		_ = conn.SetDeadline(deadline)
	}

	if err := framing.Write(conn, payload); err != nil {
		return nil, err
	}

	responsePayload, err := framing.Read(bufio.NewReader(conn))
	if err != nil {
		return nil, err
	}

	var response rpcResponse
	if err := json.Unmarshal(responsePayload, &response); err != nil {
		return nil, err
	}
	if response.Error != nil {
		return nil, fmt.Errorf("Unity error: %s", response.Error.Message)
	}
	if len(response.Result) == 0 {
		return nil, fmt.Errorf("UNITY_NO_RESPONSE")
	}

	return response.Result, nil
}
