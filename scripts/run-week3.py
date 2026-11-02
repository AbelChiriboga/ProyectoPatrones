"""Launch development API: explicit environment > User Secrets > local fallback."""
import json
import os
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def configuration_keys(value, prefix=""):
    """Match .NET's case-insensitive, colon-delimited configuration keys."""
    keys = set()
    items = value.items() if isinstance(value, dict) else enumerate(value) if isinstance(value, list) else None
    if items is None:
        return {prefix.replace("__", ":").lower()}
    for key, child in items:
        path = f"{prefix}:{key}" if prefix else str(key)
        keys.update(configuration_keys(child, path))
    return keys


def build_environment(settings, parent, user_secrets):
    env = dict(parent)
    env.setdefault("ASPNETCORE_ENVIRONMENT", "Development")
    effective_environment = env.get("DOTNET_ENVIRONMENT", env["ASPNETCORE_ENVIRONMENT"])
    secret_keys = configuration_keys(user_secrets) if effective_environment.lower() == "development" else set()
    explicit_keys = {key.replace("__", ":").lower() for key in parent}
    for key, value in settings.items():
        normalized = key.replace("__", ":").lower()
        if key != "DevelopmentAdminPassword" and normalized not in explicit_keys | secret_keys:
            env[key] = str(value)
    return env


def read_user_secrets(parent):
    project = ROOT / "backend/src/Pos.Api/Pos.Api.csproj"
    secrets_id = ET.parse(project).findtext(".//UserSecretsId")
    if not secrets_id:
        return {}
    directory = (Path(parent["APPDATA"]) / "Microsoft/UserSecrets" if parent.get("APPDATA")
                 else Path.home() / ".microsoft/usersecrets")
    path = directory / secrets_id / "secrets.json"
    return json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else {}


def main():
    local_path = ROOT / "infra/backend.env.local"
    settings = json.loads(local_path.read_text(encoding="utf-8")) if local_path.exists() else {}
    env = build_environment(settings, os.environ, read_user_secrets(os.environ))
    return subprocess.call([
        "dotnet", "run", "--no-launch-profile", "--project", "backend/src/Pos.Api",
        "--urls", "http://localhost:5038",
    ], cwd=ROOT, env=env)


if __name__ == "__main__":
    raise SystemExit(main())
