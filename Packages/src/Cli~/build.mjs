import { build } from "esbuild";

await build({
  entryPoints: ["src/cli.ts"],
  bundle: true,
  platform: "node",
  format: "cjs",
  outfile: "dist/cli.bundle.cjs",
  sourcemap: true,
  banner: { js: "#!/usr/bin/env node" },
  loader: { ".md": "text" },
});
