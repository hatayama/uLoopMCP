import express, { Request, Response } from "express";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import { z } from "zod";

const PORT = 3001;

const app = express();
app.use(express.json());

app.post("/mcp", async (req: Request, res: Response) => {
    const server = new McpServer({
        name: "streamable-http-sample",
        version: "1.0.0",
    });

    server.tool(
        "hello",
        "Returns a greeting message",
        { name: z.string().describe("Name to greet") },
        async ({ name }) => ({
            content: [{ type: "text", text: `Hello, ${name}!` }],
        })
    );

    const transport = new StreamableHTTPServerTransport({
        sessionIdGenerator: undefined,
    });

    res.on("close", () => {
        transport.close();
    });

    await server.connect(transport);
    await transport.handleRequest(req, res, req.body);
});

app.get("/mcp", (_req: Request, res: Response) => {
    res.status(405).json({
        jsonrpc: "2.0",
        error: {
            code: -32000,
            message: "Method not allowed (stateless server)",
        },
        id: null,
    });
});

app.delete("/mcp", (_req: Request, res: Response) => {
    res.status(405).json({
        jsonrpc: "2.0",
        error: {
            code: -32000,
            message: "Method not allowed (stateless server)",
        },
        id: null,
    });
});

app.get("/health", (_req: Request, res: Response) => {
    res.json({ status: "ok" });
});

app.listen(PORT, () => {
    console.log(`Streamable HTTP MCP Server running on http://localhost:${PORT}/mcp`);
    console.log(`Health check: http://localhost:${PORT}/health`);
});
