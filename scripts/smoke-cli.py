"""임시 .env와 로컬 서버로 게시된 CLI의 실제 인증·파일 전송을 검사한다."""

import argparse
import hashlib
import http.server
import json
import os
from pathlib import Path
import subprocess
import tempfile
import threading


def checksum(content):
    return hashlib.md5(content).hexdigest()


class BucketServer(http.server.BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def read_body(self):
        if self.headers.get("Transfer-Encoding") == "chunked":
            chunks = []
            while True:
                size = int(self.rfile.readline().split(b";", 1)[0], 16)
                if size == 0:
                    while True:
                        trailer = self.rfile.readline()
                        if not trailer or trailer == b"\r\n":
                            break
                    return b"".join(chunks)
                chunks.append(self.rfile.read(size))
                self.rfile.read(2)
        return self.rfile.read(int(self.headers.get("Content-Length", 0)))

    def respond(self, value):
        data = json.dumps(value).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        self.server.requests.append(self.path)
        if self.path == "/download":
            if self.headers.get("Authorization"):
                self.send_error(400, "Download must not receive the API token")
                return
            content = b"hello"
            self.send_response(200)
            self.send_header("Content-Length", str(len(content)))
            self.end_headers()
            self.wfile.write(content)
            return
        if self.path == "/api/buckets/common/bucket/files":
            self.respond({
                "id": "bucket",
                "lastUpdated": "2026-10-30T00:00:00Z",
                "files": [{
                    "path": "docs/readme.txt",
                    "location": f"http://127.0.0.1:{self.server.server_port}/download",
                    "metadata": {"size": 5, "checksum": checksum(b"hello")}
                }]
            })
            return
        self.send_error(404)

    def do_POST(self):
        self.server.requests.append(self.path)
        body = json.loads(self.read_body())
        if self.path == "/api/auth/login":
            self.respond({"token": "local-smoke-token"})
            return
        if self.headers.get("Authorization") != "Bearer local-smoke-token":
            self.send_error(401)
            return
        if self.path == "/api/buckets/common/bucket/sync":
            actions = []
            for item in body["files"]:
                uploaded = self.server.uploads.get(item["path"])
                if uploaded is None or checksum(uploaded) != item["checksum"]:
                    actions.append({"path": item["path"], "action": {
                        "type": "Http",
                        "parameters": {
                            "method": "PUT",
                            "url": f"http://127.0.0.1:{self.server.server_port}/upload/{item['path']}"
                        }
                    }})
            self.respond({"isSuccess": not actions, "requiredActions": actions, "updatedAt": "2026-10-30T00:00:00Z"})
            return
        self.send_error(404)

    def do_PUT(self):
        self.server.requests.append(self.path)
        if self.headers.get("Authorization"):
            self.send_error(400, "Upload must not receive the API token")
            return
        self.server.uploads[self.path.removeprefix("/upload/")] = self.read_body()
        self.respond({"success": True})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--report", type=Path)
    options = parser.parse_args()
    binary = options.binary.resolve()
    results = []
    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), BucketServer)
    server.requests = []
    server.uploads = {}
    threading.Thread(target=server.serve_forever, daemon=True).start()
    try:
        with tempfile.TemporaryDirectory(prefix="fish-cli-smoke-") as directory:
            base = Path(directory)
            workspace = base / "workspace"
            workspace.mkdir()
            environment = {key: value for key, value in os.environ.items() if not key.startswith("FISH_")}

            def run(label, arguments, expected, stdin=""):
                response = subprocess.run(
                    [str(binary), *arguments], cwd=base, env=environment,
                    input=stdin, capture_output=True, text=True, timeout=30
                )
                results.append({"case": label, "exit": response.returncode, "stdout": response.stdout, "stderr": response.stderr})
                assert response.returncode == expected, f"{label}: {response.stderr}"
                assert "local-smoke-password" not in response.stdout + response.stderr
                assert "local-smoke-token" not in response.stdout + response.stderr
                return response

            run("help", ["--help"], 0)
            missing = run("missing-credentials", ["pull", "bucket", "--root", str(workspace)], 1)
            assert "FISH_USERNAME" in missing.stderr and "FISH_PASSWORD" in missing.stderr
            (base / ".env").write_text(
                "FISH_USERNAME=local-smoke-user\nFISH_PASSWORD=local-smoke-password\n"
                f"FISH_SERVER=http://127.0.0.1:{server.server_port}/api\n"
                f"FISH_ROOT='{workspace}'\nFISH_BUCKET_ID=bucket\n"
            )
            run("pull", ["pull"], 0)
            target = workspace / "docs/readme.txt"
            assert target.read_bytes() == b"hello"
            target.write_bytes(b"changed content")
            (workspace / "logs").mkdir()
            (workspace / "logs/debug.txt").write_bytes(b"log")
            pushed = run("push", ["push", "-y"], 0)
            assert "logs/debug.txt" in pushed.stdout
            assert server.uploads["docs/readme.txt"] == b"changed content"
            assert server.uploads["logs/debug.txt"] == b"log"
            before = len(server.requests)
            run("cancel", ["push"], 1, "n\n")
            invalid = run("invalid-option", ["push", "--unknown"], 1)
            assert not invalid.stdout
            assert len(server.requests) == before
            run("long-yes-option", ["push", "--yes"], 0)
    finally:
        server.shutdown()
        server.server_close()
        if options.report:
            options.report.parent.mkdir(parents=True, exist_ok=True)
            options.report.write_text(json.dumps({"results": results, "http_requests": server.requests}, ensure_ascii=False, indent=2))
    print(f"Passed {len(results)} CLI smoke checks, including authenticated download and upload.")


if __name__ == "__main__":
    main()
