"""Prepare only the isolated patrones-semana3 project. Never deletes data or prints secrets."""
import json, secrets, subprocess, time
from pathlib import Path
root = Path(__file__).resolve().parents[1]
infra = root / "infra"
credentials = infra / "backend.env.local"
def run(args, **kwargs):
    return subprocess.run(args, cwd=root, check=True, text=True, capture_output=True, **kwargs).stdout
base = ["docker", "compose", "-p", "patrones-semana3", "--env-file", str(infra / ".env"), "-f", str(infra / "docker-compose.yml")]
if credentials.exists():
    print("Local credentials already exist; no initialization or key rotation performed.")
    raise SystemExit(0)
env = dict(line.split("=", 1) for line in (infra / ".env").read_text().splitlines() if "=" in line)
admin = secrets.token_hex(32)
config = infra / "odoo.env.local"
if not config.exists():
    config.write_text("[options]\ndb_host = db\ndb_user = " + env["POSTGRES_USER"] + "\ndb_password = " + env["POSTGRES_PASSWORD"] + "\nadmin_passwd = " + secrets.token_hex(32) + "\nlist_db = False\nhttp_interface = 0.0.0.0\ndb_name = pos_db\n", encoding="utf-8")
# Use override alone for ports via !override YAML to avoid merging the public binding.
override_yaml = infra / "compose.env.local"
override_yaml.write_text('services:\n  odoo:\n    ports: !override ["127.0.0.1:8069:8069"]\n    volumes:\n      - "'+str(config).replace("\\","/")+':/etc/odoo/odoo.conf:ro"\n',encoding="utf-8")
base += ["-f", str(override_yaml)]
run(base + ["up", "-d", "db"])
for attempt in range(60):
    try:
        run(base + ["exec", "-T", "db", "pg_isready", "-U", env["POSTGRES_USER"]]); break
    except subprocess.CalledProcessError: time.sleep(1)
else: raise RuntimeError("PostgreSQL readiness failed")
databases=run(base + ["exec","-T","db","psql","-U",env["POSTGRES_USER"],"-d","postgres","-tAc","SELECT datname FROM pg_database WHERE datname = 'pos_db'"])
if "pos_db" in databases:
    raise SystemExit("Existing pos_db without local credentials: recover credentials manually; initialization refused.")
print("Initializing isolated pos_db without HTTP; this can take several minutes.",flush=True)
try:
    run(base + ["run", "--rm", "--no-deps", "odoo", "odoo", "-c", "/etc/odoo/odoo.conf", "-d", "pos_db", "-i", "point_of_sale", "--without-demo=all", "--stop-after-init", "--no-http", "--log-level=warn"])
    setup = (infra / "odoo/setup_pos.py").read_text()
    setup = setup.replace("iva = env", "env.ref('base.user_admin').write({'password': " + repr(admin) + "})\niva = env",1)
    setup = setup.replace("products_data = [", "if not iva:\n    iva = env['account.tax'].create({'name':'IVA desarrollo 15%', 'amount':15.0, 'type_tax_use':'sale', 'amount_type':'percent', 'price_include_override':'tax_excluded'})\nproducts_data = [",1)
    setup += "\nprint('CONFIGID %s' % config.id)\n"
    output=run(base + ["run", "--rm", "--no-deps", "-T", "odoo", "odoo", "shell", "-c", "/etc/odoo/odoo.conf", "-d", "pos_db", "--no-http", "--log-level=warn"],input=setup)
    key=next(l[7:] for l in output.splitlines() if l.startswith("APIKEY "))
    config_id=next(l[9:] for l in output.splitlines() if l.startswith("CONFIGID "))
    credentials.write_text(json.dumps({"Odoo__ApiKey":key,"Odoo__BaseUrl":"http://localhost:8069","Odoo__Database":"pos_db","Odoo__PosConfigId":config_id,"Jwt__Key":secrets.token_hex(32),"Auth__Operators__0__Username":"cajero1","Auth__Operators__0__Password":secrets.token_hex(24),"DevelopmentAdminPassword":admin}),encoding="utf-8")
    run(base + ["up", "-d", "odoo"])
    print("Odoo prepared on loopback. Credentials saved in ignored infra/backend.env.local; values suppressed.")
except subprocess.CalledProcessError as error:
    # Odoo logs can contain sensitive material. Keep local, never print stderr wholesale.
    (infra / "prepare-error.env.local").write_text(error.stderr or "command failed",encoding="utf-8")
    print("Initialization failed; inspect ignored infra/prepare-error.env.local locally.")
    raise SystemExit(1)
