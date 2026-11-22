# ==============================================================================
# start_lan.ps1 - Asistente de Arranque y Configuración de Red Local (LAN)
# ==============================================================================
[CmdletBinding()]
param(
    [switch]$CheckFirewallOnly,
    [switch]$AddFirewallRules
)

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "   CONFIGURACION Y ARRANQUE DE POS EN RED LOCAL (LAN) - 2 COMPUTADORAS " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# 1. Deteccion de IP Local
$localIp = (Get-NetIPAddress -AddressFamily IPv4 -InterfaceAlias "Wi-Fi*", "Ethernet*", "vEthernet*" -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } |
    Select-Object -First 1).IPAddress

if (-not $localIp) {
    # Fallback usando script python
    try {
        $localIp = (py -3 scripts/detect_lan_ip.py --raw).Trim()
    } catch {
        $localIp = "127.0.0.1"
    }
}

Write-Host "`n[+] IP del Servidor detectada en la red local: " -NoNewline
Write-Host "$localIp" -ForegroundColor Yellow

# 2. Direcciones de acceso
Write-Host "`n----------------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host "  URLS DE ACCESO:" -ForegroundColor Green
Write-Host "  PC 1 (Servidor Local):"
Write-Host "    - Frontend POS:     http://localhost:5173" -ForegroundColor White
Write-Host "    - Backend Swagger:  http://localhost:5038/swagger" -ForegroundColor White
Write-Host "    - Odoo Community:   http://localhost:8069" -ForegroundColor White
Write-Host "`n  PC 2 (Cliente LAN en la misma red Wi-Fi / Ethernet):"
Write-Host "    - Frontend POS:     http://${localIp}:5173" -ForegroundColor Yellow
Write-Host "    - Backend Swagger:  http://${localIp}:5038/swagger" -ForegroundColor Yellow
Write-Host "    - Odoo Community:   http://${localIp}:8069" -ForegroundColor Yellow
Write-Host "----------------------------------------------------------------------" -ForegroundColor DarkGray

# 3. Verificacion de Firewall de Windows
Write-Host "`n[+] Verificando reglas de Firewall de Windows..." -ForegroundColor Cyan

$rule5038 = Get-NetFirewallRule -DisplayName "*PosApi*" -ErrorAction SilentlyContinue
$rule5173 = Get-NetFirewallRule -DisplayName "*PosFrontend*" -ErrorAction SilentlyContinue

if ($AddFirewallRules) {
    Write-Host "    Agregando reglas de entrada al Firewall (requiere permisos de Administrador)..."
    try {
        New-NetFirewallRule -DisplayName "PosApi-5038" -Direction Inbound -LocalPort 5038 -Protocol TCP -Action Allow -Profile Any -ErrorAction Stop | Out-Null
        New-NetFirewallRule -DisplayName "PosFrontend-5173" -Direction Inbound -LocalPort 5173 -Protocol TCP -Action Allow -Profile Any -ErrorAction Stop | Out-Null
        Write-Host "    [OK] Reglas para puertos 5038 y 5173 agregadas exitosamente." -ForegroundColor Green
    } catch {
        Write-Host "    [!] No se pudieron agregar reglas automáticamente. Ejecute PowerShell como Administrador o use:" -ForegroundColor Red
        Write-Host "        netsh advfirewall firewall add rule name=""PosApi-5038"" dir=in action=allow protocol=TCP localport=5038" -ForegroundColor Gray
        Write-Host "        netsh advfirewall firewall add rule name=""PosFrontend-5173"" dir=in action=allow protocol=TCP localport=5173" -ForegroundColor Gray
    }
} else {
    Write-Host "    Nota sobre Firewall: Si la PC Cliente no puede conectar, abra los puertos en PowerShell (Admin):" -ForegroundColor Gray
    Write-Host "    netsh advfirewall firewall add rule name=""PosApi-5038"" dir=in action=allow protocol=TCP localport=5038" -ForegroundColor DarkCyan
    Write-Host "    netsh advfirewall firewall add rule name=""PosFrontend-5173"" dir=in action=allow protocol=TCP localport=5173" -ForegroundColor DarkCyan
}

if ($CheckFirewallOnly) {
    exit 0
}

Write-Host "`n======================================================================" -ForegroundColor Cyan
Write-Host "  PASOS PARA INICIAR LOS SERVICIOS EN EL SERVIDOR:" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  1. Levantar Odoo y Postgres (si aún no están activos):"
Write-Host "     docker compose -f infra/docker-compose.yml up -d" -ForegroundColor Yellow
Write-Host "`n  2. Iniciar el Backend .NET (en una terminal):"
Write-Host "     dotnet run --project backend/src/Pos.Api" -ForegroundColor Yellow
Write-Host "`n  3. Iniciar el Frontend Vite (en otra terminal):"
Write-Host "     npm --prefix frontend run dev" -ForegroundColor Yellow
Write-Host "`n  4. Ejecutar la verificación automatizada:"
Write-Host "     py -3 scripts/verify_lan.py --server-ip $localIp" -ForegroundColor Yellow
Write-Host "======================================================================`n" -ForegroundColor Cyan
