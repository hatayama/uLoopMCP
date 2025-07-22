#!/usr/bin/env node

const { spawn } = require('child_process');
const readline = require('readline');

console.log('🚀 uLoopMCP Tool Tester');
console.log('========================');

// TypeScriptサーバーを起動
const server = spawn('npx', ['tsx', 'src/server.ts'], {
  cwd: './Packages/src/TypeScriptServer~',
  stdio: ['pipe', 'pipe', 'inherit'],
  env: {
    ...process.env,
    MCP_DEBUG: '1',
    UNITY_TCP_PORT: '8700',
    MCP_CLIENT_NAME: 'Test_Client'
  }
});

let messageId = 1;
let initialized = false;

// サーバーからのレスポンス処理
server.stdout.on('data', (data) => {
  const lines = data.toString().trim().split('\n');
  lines.forEach(line => {
    if (line.trim()) {
      try {
        const response = JSON.parse(line);
        console.log('📥 Response:', JSON.stringify(response, null, 2));
        
        // Initialize成功後にツールリスト取得
        if (response.id === 1 && response.result && !initialized) {
          initialized = true;
          console.log('✅ Initialized! Getting tools list...');
          sendMessage({
            jsonrpc: "2.0",
            id: ++messageId,
            method: "tools/list"
          });
        }
      } catch (e) {
        console.log('📄 Raw output:', line);
      }
    }
  });
});

server.on('error', (error) => {
  console.error('❌ Server error:', error);
});

// メッセージ送信関数
function sendMessage(message) {
  console.log('📤 Sending:', JSON.stringify(message, null, 2));
  server.stdin.write(JSON.stringify(message) + '\n');
}

// 初期化
console.log('🔄 Initializing MCP connection...');
sendMessage({
  jsonrpc: "2.0",
  id: messageId,
  method: "initialize",
  params: {
    protocolVersion: "2024-11-05",
    capabilities: {},
    clientInfo: {
      name: "Manual_Test_Client",
      version: "1.0.0"
    }
  }
});

// インタラクティブなツール実行
const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout
});

setTimeout(() => {
  console.log('\n🎮 Interactive Tool Testing');
  console.log('Available commands:');
  console.log('  ping           - Test Unity connection');
  console.log('  compile        - Compile Unity project'); 
  console.log('  get-logs       - Get Unity console logs');
  console.log('  clear-console  - Clear Unity console');
  console.log('  tools          - List all available tools');
  console.log('  quit           - Exit');
  console.log('');
  
  const askCommand = () => {
    rl.question('Enter tool name (or quit): ', (answer) => {
      const cmd = answer.trim().toLowerCase();
      
      if (cmd === 'quit') {
        console.log('👋 Exiting...');
        server.kill();
        rl.close();
        process.exit(0);
      }
      
      if (cmd === 'tools') {
        sendMessage({
          jsonrpc: "2.0",
          id: ++messageId,
          method: "tools/list"
        });
      } else if (cmd === 'ping') {
        sendMessage({
          jsonrpc: "2.0",
          id: ++messageId,
          method: "tools/call",
          params: {
            name: "ping",
            arguments: {}
          }
        });
      } else if (cmd === 'compile') {
        sendMessage({
          jsonrpc: "2.0", 
          id: ++messageId,
          method: "tools/call",
          params: {
            name: "compile",
            arguments: {
              ForceRecompile: false,
              TimeoutSeconds: 15
            }
          }
        });
      } else if (cmd === 'get-logs') {
        sendMessage({
          jsonrpc: "2.0",
          id: ++messageId,
          method: "tools/call", 
          params: {
            name: "get-logs",
            arguments: {
              LogType: "All",
              MaxCount: 10,
              IncludeStackTrace: false
            }
          }
        });
      } else if (cmd === 'clear-console') {
        sendMessage({
          jsonrpc: "2.0",
          id: ++messageId,
          method: "tools/call",
          params: {
            name: "clear-console",
            arguments: {
              AddConfirmationMessage: true
            }
          }
        });
      } else {
        console.log('❓ Unknown command. Try: ping, compile, get-logs, clear-console, tools, or quit');
      }
      
      setTimeout(askCommand, 1000); // 1秒後に次のコマンド入力を待つ
    });
  };
  
  askCommand();
}, 3000); // 3秒待ってからインタラクティブモード開始