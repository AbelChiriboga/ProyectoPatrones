#!/usr/bin/env python3
"""
detect_lan_ip.py
Detecta la dirección IPv4 local activa en la red de área local (LAN).
Compatible con Windows, Linux y macOS.
"""

import socket
import sys

def get_lan_ip() -> str:
    """
    Obtiene la dirección IP primaria utilizada para enrutar tráfico hacia la red externa/LAN.
    Usa un socket UDP sin enviar paquetes a la red para determinar la interfaz activa.
    """
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        # 10.255.255.255 o 8.8.8.8 son direcciones no enrutables localmente que fuerzan la elección de interfaz
        s.connect(('10.255.255.255', 1))
        ip = s.getsockname()[0]
    except Exception:
        try:
            # Fallback a nombre de host
            ip = socket.gethostbyname(socket.gethostname())
        except Exception:
            ip = '127.0.0.1'
    finally:
        s.close()
    return ip

if __name__ == '__main__':
    ip = get_lan_ip()
    if len(sys.argv) > 1 and sys.argv[1] == '--raw':
        print(ip)
    else:
        print(f"Direccion IP detectada en LAN: {ip}")
