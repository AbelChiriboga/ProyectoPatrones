#!/usr/bin/env python3
"""
verify_lan.py
Verifica la conectividad de red, puertos, CORS y endpoints HTTP entre PC Servidor y PC Cliente en LAN.
Puede ejecutarse desde la misma máquina servidora o desde una segunda máquina en la red local.

Uso:
    py -3 scripts/verify_lan.py [--server-ip 192.168.1.34] [--api-port 5038] [--ui-port 5173] [--report docs/evidencias/lan_verification_report.json]
"""

import argparse
import json
import os
import socket
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone

# Intentar importar detector de IP local
try:
    from detect_lan_ip import get_lan_ip
except ImportError:
    def get_lan_ip():
        return '127.0.0.1'


def check_tcp_port(host: str, port: int, timeout_sec: float = 3.0) -> dict:
    """Verifica si un puerto TCP está abierto y aceptando conexiones."""
    start = time.perf_counter()
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.settimeout(timeout_sec)
    try:
        s.connect((host, port))
        elapsed_ms = (time.perf_counter() - start) * 1000.0
        return {"status": "PASS", "open": True, "latency_ms": round(elapsed_ms, 2), "error": None}
    except Exception as e:
        elapsed_ms = (time.perf_counter() - start) * 1000.0
        return {"status": "FAIL", "open": False, "latency_ms": round(elapsed_ms, 2), "error": str(e)}
    finally:
        s.close()


def check_http_endpoint(url: str, method: str = 'GET', headers: dict = None, timeout_sec: float = 5.0) -> dict:
    """Ejecuta una petición HTTP y registra código de estado, cabeceras y tiempo de respuesta."""
    headers = headers or {}
    start = time.perf_counter()
    req = urllib.request.Request(url, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=timeout_sec) as resp:
            elapsed_ms = (time.perf_counter() - start) * 1000.0
            body = resp.read().decode('utf-8', errors='ignore')
            resp_headers = {k.lower(): v for k, v in resp.headers.items()}
            return {
                "status": "PASS" if 200 <= resp.status < 400 else "FAIL",
                "http_status": resp.status,
                "latency_ms": round(elapsed_ms, 2),
                "headers": resp_headers,
                "body_snippet": body[:200],
                "error": None
            }
    except urllib.error.HTTPError as e:
        elapsed_ms = (time.perf_counter() - start) * 1000.0
        resp_headers = {k.lower(): v for k, v in e.headers.items()} if hasattr(e, 'headers') else {}
        body = e.read().decode('utf-8', errors='ignore') if hasattr(e, 'read') else ''
        return {
            "status": "PASS" if e.code in [200, 204, 401, 403] else "FAIL",
            "http_status": e.code,
            "latency_ms": round(elapsed_ms, 2),
            "headers": resp_headers,
            "body_snippet": body[:200],
            "error": f"HTTP {e.code}: {e.reason}"
        }
    except Exception as e:
        elapsed_ms = (time.perf_counter() - start) * 1000.0
        return {
            "status": "FAIL",
            "http_status": 0,
            "latency_ms": round(elapsed_ms, 2),
            "headers": {},
            "body_snippet": "",
            "error": str(e)
        }


def check_cors_preflight(base_url: str, client_origin: str) -> dict:
    """Verifica el handshake CORS (OPTIONS preflight) para un origen cliente en la LAN."""
    url = f"{base_url.rstrip('/')}/api/v1/auth/login"
    headers = {
        "Origin": client_origin,
        "Access-Control-Request-Method": "POST",
        "Access-Control-Request-Headers": "Content-Type,Authorization"
    }
    result = check_http_endpoint(url, method='OPTIONS', headers=headers)
    
    allow_origin = result.get("headers", {}).get("access-control-allow-origin", "")
    cors_passed = (
        result["http_status"] in [200, 204] and
        (allow_origin == client_origin or allow_origin == "*")
    )
    result["cors_passed"] = cors_passed
    result["allow_origin_header"] = allow_origin
    result["status"] = "PASS" if cors_passed else "FAIL"
    return result


def run_lan_verification(server_ip: str, client_ip: str, api_port: int, ui_port: int, odoo_port: int, report_file: str = None) -> dict:
    timestamp = datetime.now(timezone.utc).isoformat()
    print("=" * 72)
    print("   VERIFICACION DE DESPLIEGUE Y CONECTIVIDAD EN RED LOCAL (LAN)")
    print("=" * 72)
    print(f"Timestamp:       {timestamp}")
    print(f"PC Servidor IP:  {server_ip}")
    print(f"PC Cliente IP:   {client_ip}")
    print(f"Puerto API:      {api_port} (Kestrel .NET)")
    print(f"Puerto Frontend: {ui_port} (Vite React)")
    print(f"Puerto Odoo:     {odoo_port} (Odoo Community)")
    print("-" * 72)

    results = {
        "timestamp": timestamp,
        "server_ip": server_ip,
        "client_ip": client_ip,
        "ports": {},
        "http_checks": {},
        "overall_status": "PASS"
    }

    # 1. Comprobación de puertos TCP
    print("[1/5] Comprobando disponibilidad de puertos TCP en el Servidor...")
    for name, port in [("Backend API", api_port), ("Frontend UI", ui_port), ("Odoo POS", odoo_port)]:
        res = check_tcp_port(server_ip, port)
        results["ports"][name] = res
        sym = "[OK]" if res["status"] == "PASS" else "[FAIL]"
        err_msg = f" - Error: {res['error']}" if res["error"] else f" ({res['latency_ms']} ms)"
        print(f"  {sym} {name:<14} puerto {port}: {res['status']}{err_msg}")
        if res["status"] == "FAIL" and name != "Odoo POS":
            results["overall_status"] = "FAIL"

    # 2. Comprobación de especificación OpenAPI / Swagger
    print("\n[2/5] Verificando endpoint OpenAPI / Swagger en API .NET...")
    openapi_url = f"http://{server_ip}:{api_port}/openapi/v1.json"
    res_openapi = check_http_endpoint(openapi_url)
    results["http_checks"]["openapi_spec"] = res_openapi
    sym = "[OK]" if res_openapi["status"] == "PASS" else "[FAIL]"
    print(f"  {sym} GET {openapi_url}: Status {res_openapi['http_status']} ({res_openapi['latency_ms']} ms)")
    if res_openapi["status"] == "FAIL":
        results["overall_status"] = "FAIL"

    # 3. Comprobación de encabezados de versionamiento
    print("\n[3/5] Verificando encabezados de versionamiento (X-Api-Version)...")
    disc_url = f"http://{server_ip}:{api_port}/api/v1/discounts"
    res_disc = check_http_endpoint(disc_url)
    results["http_checks"]["api_versioning"] = res_disc
    headers = res_disc.get("headers", {})
    v_hdr = headers.get("x-api-version", "ausente")
    sup_hdr = headers.get("api-supported-versions", "ausente")
    sym = "[OK]" if v_hdr == "1.0" else "[WARN]"
    print(f"  {sym} GET {disc_url}: Status {res_disc['http_status']}")
    print(f"       X-Api-Version: {v_hdr} | api-supported-versions: {sup_hdr}")

    # 4. Comprobación de política CORS para origen cliente en LAN
    print(f"\n[4/5] Verificando política CORS para origen cliente http://{client_ip}:{ui_port}...")
    client_origin = f"http://{client_ip}:{ui_port}"
    res_cors = check_cors_preflight(f"http://{server_ip}:{api_port}", client_origin)
    results["http_checks"]["cors_preflight"] = res_cors
    sym = "[OK]" if res_cors.get("cors_passed") else "[FAIL]"
    print(f"  {sym} OPTIONS /api/v1/auth/login desde {client_origin}")
    print(f"       Access-Control-Allow-Origin: {res_cors.get('allow_origin_header', 'ausente')}")
    print(f"       Resultado CORS: {'PERMITIDO' if res_cors.get('cors_passed') else 'DENEGADO'}")
    if not res_cors.get("cors_passed"):
        results["overall_status"] = "FAIL"

    # 5. Comprobación de carga del Frontend
    print(f"\n[5/5] Verificando acceso web al Frontend UI...")
    ui_url = f"http://{server_ip}:{ui_port}/"
    res_ui = check_http_endpoint(ui_url)
    results["http_checks"]["frontend_ui"] = res_ui
    sym = "[OK]" if res_ui["status"] == "PASS" else "[FAIL]"
    print(f"  {sym} GET {ui_url}: Status {res_ui['http_status']} ({res_ui['latency_ms']} ms)")
    if res_ui["status"] == "FAIL":
        results["overall_status"] = "FAIL"

    print("\n" + "=" * 72)
    overall_sym = "[APROBADO]" if results["overall_status"] == "PASS" else "[REQUIERE ATENCION]"
    print(f"  ESTADO GENERAL DE LA PRUEBA LAN: {overall_sym}")
    print("=" * 72)

    if report_file:
        os.makedirs(os.path.dirname(os.path.abspath(report_file)), exist_ok=True)
        with open(report_file, 'w', encoding='utf-8') as f:
            json.dump(results, f, indent=2, ensure_ascii=False)
        print(f"\nEvidencia guardada exitosamente en: {report_file}")

    return results


def main():
    parser = argparse.ArgumentParser(description="Verificador de despliegue en red local (LAN)")
    detected_ip = get_lan_ip()
    parser.add_argument("--server-ip", default=detected_ip, help=f"IP del Servidor (por defecto: {detected_ip})")
    parser.add_argument("--client-ip", default=detected_ip, help=f"IP del Cliente para prueba CORS (por defecto: {detected_ip})")
    parser.add_argument("--api-port", type=int, default=5038, help="Puerto del API .NET (5038)")
    parser.add_argument("--ui-port", type=int, default=5173, help="Puerto del Frontend Vite (5173)")
    parser.add_argument("--odoo-port", type=int, default=8069, help="Puerto de Odoo (8069)")
    parser.add_argument("--report", default=os.path.join("docs", "evidencias", "lan_verification_report.json"),
                        help="Ruta donde guardar el reporte JSON de evidencia")
    args = parser.parse_args()

    results = run_lan_verification(
        server_ip=args.server_ip,
        client_ip=args.client_ip,
        api_port=args.api_port,
        ui_port=args.ui_port,
        odoo_port=args.odoo_port,
        report_file=args.report
    )
    # No fallar con exit 1 si los servicios locales aún no están levantados al momento de configurar
    # sino emitir el reporte para que el usuario o el script de verificación lo consuma


if __name__ == '__main__':
    main()
