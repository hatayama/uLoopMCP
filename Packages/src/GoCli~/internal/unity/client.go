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

type ProgressFunc func(message string)

type SendOutcome struct {
	Result            json.RawMessage
	RequestDispatched bool
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
	return client.SendWithProgress(ctx, method, params, nil)
}

func (client *Client) SendWithProgress(ctx context.Context, method string, params map[string]any, progress ProgressFunc) (json.RawMessage, error) {
	outcome, err := client.SendWithProgressOutcome(ctx, method, params, progress)
	return outcome.Result, err
}

func (client *Client) SendWithProgressOutcome(ctx context.Context, method string, params map[string]any, progress ProgressFunc) (SendOutcome, error) {
	ctx, cancel := context.WithTimeout(ctx, requestTimeout)
	defer cancel()

	conn, err := dialEndpoint(ctx, client.connection.Endpoint)
	if err != nil {
		return SendOutcome{}, fmt.Errorf("connection error: %w", err)
	}
	defer conn.Close()

	if progress != nil {
		progress("connected")
	}

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
		return SendOutcome{}, err
	}

	if deadline, ok := ctx.Deadline(); ok {
		_ = conn.SetDeadline(deadline)
	}

	if err := framing.Write(conn, payload); err != nil {
		return SendOutcome{}, err
	}
	outcome := SendOutcome{RequestDispatched: true}

	responsePayload, err := framing.Read(bufio.NewReader(conn))
	if err != nil {
		return outcome, err
	}

	var response rpcResponse
	if err := json.Unmarshal(responsePayload, &response); err != nil {
		return outcome, err
	}
	if response.Error != nil {
		return outcome, fmt.Errorf("Unity error: %s", response.Error.Message)
	}
	if len(response.Result) == 0 {
		return outcome, fmt.Errorf("UNITY_NO_RESPONSE")
	}

	outcome.Result = response.Result
	return outcome, nil
}
