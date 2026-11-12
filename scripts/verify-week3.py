"""Read checks by default. --write authorizes one traceable P09 intention on isolated local pos_db."""
import json, os, subprocess, sys, time, urllib.request, urllib.error, uuid
from pathlib import Path
root=Path(__file__).resolve().parents[1]
secrets=json.loads((root/'infra/backend.env.local').read_text())
assert secrets['Odoo__BaseUrl'] in ('http://localhost:8069','http://127.0.0.1:8069') and secrets['Odoo__Database']=='pos_db'
env=os.environ.copy();env.update({k:str(v) for k,v in secrets.items() if k!='DevelopmentAdminPassword'})
env['ASPNETCORE_ENVIRONMENT']='Development'
# Read-only integration tests; never silently substitute a skipped suite.
subprocess.run(['dotnet','test','backend/PosOdoo.slnx','--no-restore'],cwd=root,env=env,check=True)
log=(root/'infra/api.env.local').open('w',encoding='utf-8')
api=subprocess.Popen(['dotnet','run','--no-build','--no-launch-profile','--project','backend/src/Pos.Api','--urls','http://127.0.0.1:5049'],cwd=root,env=env,stdout=log,stderr=log)
token=None
def call(path,body=None):
 headers={'Content-Type':'application/json'}
 if token:headers['Authorization']='Bearer '+token
 request=urllib.request.Request('http://127.0.0.1:5049'+path,data=None if body is None else json.dumps(body).encode(),headers=headers)
 try:
  with urllib.request.urlopen(request,timeout=60) as response:return json.load(response)
 except urllib.error.HTTPError as error:
  problem=json.load(error); raise RuntimeError(str(error.code)+' '+str(problem.get('code'))+' '+str(problem.get('detail')))
try:
 for attempt in range(60):
  try:
   login=call('/api/auth/login',{'username':secrets['Auth__Operators__0__Username'],'password':secrets['Auth__Operators__0__Password']});break
  except (OSError,RuntimeError):time.sleep(1)
 else:raise RuntimeError('API readiness failed; inspect ignored infra/api.env.local')
 token=login['token']
 session=call('/api/pos/sessions/active');assert session['configId']==int(secrets['Odoo__PosConfigId'])
 products=call('/api/products');assert len(products)>=2
 request={'sessionId':session['id'],'lines':[{'productId':p['id'],'quantity':1} for p in products[:2]],'discountCode':'none'}
 quote=call('/api/sales/quote',request);assert quote['methods']
 print('Read-only API quote verified:',quote['total'],quote['currency'])
 evidence=root/'docs/evidencias/semana3-p09.json'
 if '--write' not in sys.argv:
  print('P09 not requested; use --write for the single traceable operation.');raise SystemExit(0)
 if evidence.exists() and '--after-verified-rollback' in sys.argv:
  old=json.loads(evidence.read_text())
  req=urllib.request.Request(secrets['Odoo__BaseUrl']+'/json/2/pos.order/search_count',data=json.dumps({'domain':[['uuid','=',old['request']['operationId']]]}).encode(),headers={'Content-Type':'application/json','X-Odoo-Database':'pos_db','Authorization':'bearer '+secrets['Odoo__ApiKey']})
  with urllib.request.urlopen(req) as response: assert json.load(response)==0
  assert old.get('definitiveRejectionHttp')==422, 'Requires observed definitive rejection, not merely absent UUID.'
  evidence.rename(evidence.with_name('semana3-p09-rejected.json'))
 if evidence.exists():
  data=json.loads(evidence.read_text());payload=data['request']
  result=call('/api/sales/operations/'+payload['operationId'])
 else:
  payload={'operationId':str(uuid.uuid4()),'sale':request,'paymentMethodId':quote['methods'][0]['id'],'expectedTotal':quote['total']}
  data={'host':secrets['Odoo__BaseUrl'],'database':'pos_db','sessionId':session['id'],'request':payload}
  evidence.write_text(json.dumps(data,indent=2),encoding='utf-8')
  result=call('/api/sales',payload)
 data['result']=result;evidence.write_text(json.dumps(data,indent=2),encoding='utf-8')
 assert result['status']=='confirmed', 'Uncertain; preserve operation and reconcile. Never send a new UUID.'
 repeated=call('/api/sales',payload);assert repeated==result
 detail=call('/api/sales/'+str(result['sale']['id']));assert detail==result['sale']
 history=call('/api/sales');assert any(s['id']==detail['id'] for s in history)
 req=urllib.request.Request(secrets['Odoo__BaseUrl']+'/json/2/pos.order/search_count',data=json.dumps({'domain':[['uuid','=',payload['operationId']]]}).encode(),headers={'Content-Type':'application/json','X-Odoo-Database':'pos_db','Authorization':'bearer '+secrets['Odoo__ApiKey']})
 with urllib.request.urlopen(req) as response:count=json.load(response)
 assert count==1
 raw={}
 for model,domain,fields in [
  ('pos.order',[['uuid','=',payload['operationId']]],['id','uuid','state','amount_total','amount_tax','amount_paid','session_id','lines','payment_ids']),
  ('pos.order.line',[['order_id','=',detail['id']]],['product_id','qty','price_unit','discount','price_subtotal','price_subtotal_incl']),
  ('pos.payment',[['pos_order_id','=',detail['id']]],['payment_method_id','amount'])]:
  req=urllib.request.Request(secrets['Odoo__BaseUrl']+'/json/2/'+model+'/search_read',data=json.dumps({'domain':domain,'fields':fields}).encode(),headers={'Content-Type':'application/json','X-Odoo-Database':'pos_db','Authorization':'bearer '+secrets['Odoo__ApiKey']})
  with urllib.request.urlopen(req) as response:raw[model]=json.load(response)
 assert len(raw['pos.order'])==1 and len(raw['pos.order.line'])==2 and len(raw['pos.payment'])==1
 data['odooReadback']=raw
 data.update({'repeatResult':repeated,'detail':detail,'uuidCount':count,'historyContainsSale':True,'verifiedUtc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())})
 evidence.write_text(json.dumps(data,indent=2),encoding='utf-8')
 print('P09 verified: paid order',detail['id'],'UUID count',count,'total',detail['total'],detail['currency'])
finally:
 api.terminate()
 try:api.wait(timeout=10)
 except subprocess.TimeoutExpired:api.kill();api.wait()
 log.close()
