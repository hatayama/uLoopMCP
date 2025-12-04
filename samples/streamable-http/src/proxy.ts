import express, { Request, Response } from "express";

const PROXY_PORT = 3000;
const BACKEND_URL = "http://localhost:3001";

const app = express();
app.use(express.json());

async function checkBackendHealth(): Promise<boolean> {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 2000);

    try {
        const response = await fetch(`${BACKEND_URL}/health`, {
            signal: controller.signal,
        });
        clearTimeout(timeout);
        return response.ok;
    } catch {
        clearTimeout(timeout);
        return false;
    }
}

app.post("/mcp", async (req: Request, res: Response) => {
    const isAlive = await checkBackendHealth();

    if (!isAlive) {
        console.log(`[Proxy] Backend is down, returning temporary error`);
        res.setHeader("Content-Type", "application/json");
        res.status(200).json({
            jsonrpc: "2.0",
            result: {
                content: [
                    {
                        type: "text",
                        text: "MCP server temporarily unavailable. Please try again later.",
                    },
                ],
            },
            id: req.body?.id ?? null,
        });
        return;
    }

    try {
        const response = await fetch(`${BACKEND_URL}/mcp`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Accept": "application/json, text/event-stream",
            },
            body: JSON.stringify(req.body),
        });

        const contentType = response.headers.get("content-type") || "application/json";
        res.setHeader("Content-Type", contentType);
        res.status(response.status);

        const text = await response.text();
        res.send(text);
    } catch (error) {
        console.error(`[Proxy] Error forwarding request:`, error);
        res.status(200).json({
            jsonrpc: "2.0",
            result: {
                content: [
                    {
                        type: "text",
                        text: "Failed to connect to MCP server. Please try again later.",
                    },
                ],
            },
            id: req.body?.id ?? null,
        });
    }
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

app.get("/health", async (_req: Request, res: Response) => {
    const backendAlive = await checkBackendHealth();
    res.json({
        status: "ok",
        proxy: true,
        backend: backendAlive ? "up" : "down",
    });
});

app.listen(PROXY_PORT, () => {
    console.log(`[Proxy] Running on http://localhost:${PROXY_PORT}/mcp`);
    console.log(`[Proxy] Forwarding to ${BACKEND_URL}/mcp`);
    console.log(`[Proxy] Health check: http://localhost:${PROXY_PORT}/health`);
});
