#!/bin/bash

# Quick MCP Tool Test Script
# Usage: ./quick-tool-test.sh <tool_name> [arguments]

echo "🚀 Quick MCP Tool Test"

# 環境変数設定
export MCP_DEBUG=1
export UNITY_TCP_PORT=8700
export MCP_CLIENT_NAME="Quick_Test"

cd Packages/src/TypeScriptServer~

# TypeScriptサーバーをバックグラウンド起動
npx tsx src/server.ts &
SERVER_PID=$!

echo "📡 Server started (PID: $SERVER_PID)"
echo "⏳ Waiting for server initialization..."

sleep 3

# テスト関数
test_tool() {
    local tool_name=$1
    local arguments=$2
    
    echo "🔧 Testing tool: $tool_name"
    
    # Initialize
    echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "Quick_Test", "version": "1.0.0"}}}' | nc localhost 3000 2>/dev/null &
    
    sleep 1
    
    # Call tool
    if [ -z "$arguments" ]; then
        echo "{\"jsonrpc\": \"2.0\", \"id\": 2, \"method\": \"tools/call\", \"params\": {\"name\": \"$tool_name\", \"arguments\": {}}}"
    else
        echo "{\"jsonrpc\": \"2.0\", \"id\": 2, \"method\": \"tools/call\", \"params\": {\"name\": \"$tool_name\", \"arguments\": $arguments}}"
    fi
}

# クリーンアップ関数
cleanup() {
    echo "🧹 Cleaning up..."
    kill $SERVER_PID 2>/dev/null
    exit 0
}

# Ctrl+C でクリーンアップ
trap cleanup SIGINT

case "$1" in
    "ping")
        test_tool "ping"
        ;;
    "compile")
        test_tool "compile" '{"ForceRecompile": false, "TimeoutSeconds": 15}'
        ;;
    "get-logs")
        test_tool "get-logs" '{"LogType": "All", "MaxCount": 5, "IncludeStackTrace": false}'
        ;;
    "clear-console")
        test_tool "clear-console" '{"AddConfirmationMessage": true}'
        ;;
    *)
        echo "Usage: $0 <tool_name>"
        echo "Available tools: ping, compile, get-logs, clear-console"
        cleanup
        ;;
esac

echo "⏳ Waiting for response... (Press Ctrl+C to exit)"
sleep 10
cleanup