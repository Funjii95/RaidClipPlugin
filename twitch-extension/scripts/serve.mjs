import https from "node:https";
import { readFileSync, existsSync, mkdirSync, writeFileSync } from "node:fs";
import { resolve, extname, sep } from "node:path";
import selfsigned from "selfsigned";
// Serve the compiled build: no inline Vite/HMR scripts in Twitch's iframe.
const certDir = resolve(".certs");
mkdirSync(certDir, { recursive: true });
if (
  !existsSync(resolve(certDir, "localhost.pem")) ||
  !existsSync(resolve(certDir, "localhost-key.pem"))
) {
  const pems = await selfsigned.generate(
    [{ name: "commonName", value: "localhost" }],
    {
      days: 365,
      algorithm: "sha256",
      keySize: 2048,
      extensions: [
        {
          name: "subjectAltName",
          altNames: [
            { type: 2, value: "localhost" },
            { type: 7, ip: "127.0.0.1" },
          ],
        },
      ],
    },
  );
  writeFileSync(resolve(certDir, "localhost.pem"), pems.cert);
  writeFileSync(resolve(certDir, "localhost-key.pem"), pems.private);
}
const root = resolve("dist");
https
  .createServer(
    {
      cert: readFileSync(resolve(certDir, "localhost.pem")),
      key: readFileSync(resolve(certDir, "localhost-key.pem")),
    },
    (req, res) => {
      let file;
      try {
        const path = new URL(req.url, "https://localhost").pathname;
        file = resolve(
          root,
          "." + (path === "/" ? "/panel.html" : decodeURIComponent(path)),
        );
      } catch {
        res.writeHead(400);
        res.end();
        return;
      }
      if (!file.startsWith(root + sep)) {
        res.writeHead(403);
        res.end();
        return;
      }
      try {
        const data = readFileSync(file);
        res.setHeader(
          "Content-Type",
          {
            ".html": "text/html; charset=utf-8",
            ".js": "text/javascript",
            ".css": "text/css",
            ".svg": "image/svg+xml",
            ".png": "image/png",
            ".webp": "image/webp",
          }[extname(file)] ?? "application/octet-stream",
        );
        res.setHeader(
          "Content-Security-Policy",
          "default-src 'self'; script-src 'self' https://extension-files.twitch.tv; style-src 'self'; img-src 'self' data:; connect-src https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app; object-src 'none'; base-uri 'none'",
        );
        res.setHeader("X-Content-Type-Options", "nosniff");
        res.end(data);
      } catch {
        res.writeHead(404);
        res.end("Nicht gefunden");
      }
    },
  )
  .listen(8080, "localhost", () =>
    console.log(
      "Demo: https://localhost:8080/panel.html (nur lokal; Zertifikat siehe README)",
    ),
  );
