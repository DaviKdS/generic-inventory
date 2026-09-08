#!/usr/bin/env python3
"""Start Generic Inventory with an automatic public Cloudflare Quick Tunnel."""

from __future__ import annotations

import argparse
import os
import re
import shutil
import socket
import subprocess
import sys
import threading
import time
import urllib.request
import webbrowser
from pathlib import Path


ROOT = Path(__file__).resolve().parent
DEFAULT_PORT = int(os.environ.get("PORT", "5045"))
APP_DLL = ROOT / "bin" / "Debug" / "net8.0" / "GenericInventory.dll"
APP_DATA = ROOT / "App_Data"
PUBLIC_URL_FILE = APP_DATA / "current-public-url.txt"
CLOUDFLARED_URL = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"
TUNNEL_URL_PATTERN = re.compile(r"https://[a-z0-9-]+\.trycloudflare\.com", re.IGNORECASE)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Start the site and create a temporary public domain.")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT, help=f"Local port. Default: {DEFAULT_PORT}.")
    parser.add_argument("--build", action="store_true", help="Run dotnet build before starting.")
    parser.add_argument("--no-browser", action="store_true", help="Do not open the public URL automatically.")
    parser.add_argument("--cloudflared", default="", help="Optional path to cloudflared.exe.")
    return parser.parse_args()


def find_dotnet() -> str:
    dotnet = shutil.which("dotnet")
    if dotnet:
        return dotnet

    local_dotnet = Path.home() / ".dotnet" / "dotnet.exe"
    if local_dotnet.exists():
        return str(local_dotnet)

    raise SystemExit("dotnet was not found. Install .NET 8 or run the installer used earlier in this project.")


def find_or_install_cloudflared(configured_path: str) -> str:
    if configured_path:
        path = Path(configured_path)
        if path.exists():
            return str(path)
        raise SystemExit(f"cloudflared was not found at: {configured_path}")

    cloudflared = shutil.which("cloudflared")
    if cloudflared:
        return cloudflared

    tools_dir = Path.home() / ".codex" / "tools"
    tools_dir.mkdir(parents=True, exist_ok=True)
    local_cloudflared = tools_dir / "cloudflared.exe"

    if not local_cloudflared.exists():
        print("Downloading cloudflared...")
        urllib.request.urlretrieve(CLOUDFLARED_URL, local_cloudflared)

    return str(local_cloudflared)


def port_is_open(host: str, port: int) -> bool:
    try:
        with socket.create_connection((host, port), timeout=1):
            return True
    except OSError:
        return False


def wait_until_ready(url: str, timeout_seconds: int = 40) -> bool:
    deadline = time.monotonic() + timeout_seconds
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=2) as response:
                return 200 <= response.status < 500
        except OSError:
            time.sleep(0.5)
    return False


def run(command: list[str]) -> None:
    completed = subprocess.run(command, cwd=ROOT, check=False)
    if completed.returncode != 0:
        raise SystemExit(completed.returncode)


def start_tunnel(cloudflared: str, port: int) -> tuple[subprocess.Popen[str], str]:
    command = [cloudflared, "tunnel", "--url", f"http://localhost:{port}"]
    process = subprocess.Popen(
        command,
        cwd=ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
    )

    public_url = ""
    lines: list[str] = []
    deadline = time.monotonic() + 90

    while time.monotonic() < deadline:
        line = process.stdout.readline() if process.stdout else ""
        if line:
            print(line, end="")
            lines.append(line)
            match = TUNNEL_URL_PATTERN.search(line)
            if match:
                public_url = match.group(0)
                break

        if process.poll() is not None:
            raise SystemExit("cloudflared stopped before creating a public URL.")

    if not public_url:
        process.terminate()
        raise SystemExit("Could not detect a trycloudflare.com URL in cloudflared output.")

    threading.Thread(target=pipe_output, args=(process,), daemon=True).start()
    return process, public_url


def pipe_output(process: subprocess.Popen[str]) -> None:
    if not process.stdout:
        return

    for line in process.stdout:
        print(line, end="")


def start_server(dotnet: str, port: int, public_url: str, build: bool) -> subprocess.Popen[str]:
    if port_is_open("localhost", port):
        raise SystemExit(
            f"Port {port} is already in use. Stop the old server first so the new public URL can be applied."
        )

    if build or not APP_DLL.exists():
        print("Building project...")
        run([dotnet, "build"])

    env = os.environ.copy()
    env["App__PublicBaseUrl"] = public_url

    command = [dotnet, "run", "--no-build", "--no-launch-profile", "--urls", f"http://0.0.0.0:{port}"]
    return subprocess.Popen(command, cwd=ROOT, env=env)


def save_public_url(public_url: str) -> None:
    APP_DATA.mkdir(parents=True, exist_ok=True)
    PUBLIC_URL_FILE.write_text(public_url + "\n", encoding="utf-8")


def main() -> int:
    args = parse_args()
    dotnet = find_dotnet()
    cloudflared = find_or_install_cloudflared(args.cloudflared)

    print("Creating public tunnel...")
    tunnel_process, public_url = start_tunnel(cloudflared, args.port)
    save_public_url(public_url)

    print(f"\nPublic URL: {public_url}")
    print(f"Saved to: {PUBLIC_URL_FILE}")
    print("Starting ASP.NET with App__PublicBaseUrl adjusted to the public URL...")

    server_process: subprocess.Popen[str] | None = None
    try:
        server_process = start_server(dotnet, args.port, public_url, args.build)
        local_url = f"http://localhost:{args.port}"

        if wait_until_ready(local_url):
            print(f"Ready locally: {local_url}")
            print(f"Ready publicly: {public_url}")
            if not args.no_browser:
                webbrowser.open(public_url)
        else:
            print("Server did not respond before the readiness timeout.", file=sys.stderr)

        return server_process.wait()
    except KeyboardInterrupt:
        print("\nStopping public server...")
        return 0
    finally:
        for process in (server_process, tunnel_process):
            if process and process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()


if __name__ == "__main__":
    raise SystemExit(main())
