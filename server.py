#!/usr/bin/env python3
"""Start the local Generic Inventory ASP.NET server."""

from __future__ import annotations

import argparse
import os
import shutil
import socket
import subprocess
import sys
import time
import urllib.request
import webbrowser
from pathlib import Path


ROOT = Path(__file__).resolve().parent
DEFAULT_PORT = int(os.environ.get("PORT", "5045"))
APP_DLL = ROOT / "bin" / "Debug" / "net8.0" / "GenericInventory.dll"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Start the generic inventory site.")
    parser.add_argument("--host", default="localhost", help="Host used by ASP.NET. Default: localhost.")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT, help=f"Port to listen on. Default: {DEFAULT_PORT}.")
    parser.add_argument("--build", action="store_true", help="Run dotnet build before starting.")
    parser.add_argument("--no-browser", action="store_true", help="Do not open the browser automatically.")
    return parser.parse_args()


def ensure_dotnet() -> None:
    if shutil.which("dotnet") is None:
        raise SystemExit("dotnet was not found in PATH. Install the .NET SDK/runtime first.")


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


def main() -> int:
    args = parse_args()
    ensure_dotnet()

    url = f"http://{args.host}:{args.port}"
    if port_is_open(args.host, args.port):
        print(f"Server already appears to be running at {url}")
        if not args.no_browser:
            webbrowser.open(url)
        return 0

    if args.build or not APP_DLL.exists():
        print("Building project...")
        run(["dotnet", "build"])

    command = ["dotnet", "run", "--no-build", "--urls", url]
    print(f"Starting {url}")
    process = subprocess.Popen(command, cwd=ROOT)

    try:
        if wait_until_ready(url):
            print(f"Ready: {url}")
            if not args.no_browser:
                webbrowser.open(url)
        else:
            print("Server did not respond before the readiness timeout.", file=sys.stderr)

        return process.wait()
    except KeyboardInterrupt:
        print("\nStopping server...")
        process.terminate()
        try:
            return process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            return process.wait()


if __name__ == "__main__":
    raise SystemExit(main())
