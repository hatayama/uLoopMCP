const { spawn } = require('child_process');

// Start the MCP server
const server = spawn('node', ['dist/server.bundle.js'], {
  stdio: ['pipe', 'pipe', 'pipe']
});

// Send initialize request
const initRequest = {
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2024-11-05",
    "capabilities": {
      "tools": {}
    },
    "clientInfo": {
      "name": "windsurf",
      "version": "1.0.0"
    }
  }
};

console.log('Sending initialize request:', JSON.stringify(initRequest));
server.stdin.write(JSON.stringify(initRequest) + '\n');

// Send tools/list request after a short delay
setTimeout(() => {
  const toolsRequest = {
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list",
    "params": {}
  };
  
  console.log('Sending tools/list request:', JSON.stringify(toolsRequest));
  server.stdin.write(JSON.stringify(toolsRequest) + '\n');
  
  // End after another delay
  setTimeout(() => {
    server.stdin.end();
  }, 2000);
}, 1000);

server.stdout.on('data', (data) => {
  console.log('Server stdout:', data.toString());
});

server.stderr.on('data', (data) => {
  console.error('Server stderr:', data.toString());
});

server.on('exit', (code) => {
  console.log('Server exited with code:', code);
});
EOF < /dev/null