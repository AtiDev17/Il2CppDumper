import json
from ghidra.program.model.symbol import SourceType
from ghidra.program.model.data import DataTypeConflictHandler, CategoryPath, StructureDataType, ParameterDefinitionImpl
from ghidra.program.model.listing import ParameterImpl, Function
from java.util import ArrayList as JArrayList

try:
	from ghidra.program.model.data import VoidDataType
	_VOID = VoidDataType.dataType
except:
	_VOID = None

baseAddress = currentProgram.getImageBase()
dtm = currentProgram.getDataTypeManager()
handler = DataTypeConflictHandler.REPLACE_HANDLER

_type_cache = {}
_find_cache = {}

for dt in dtm.getAllDataTypes():
	_find_cache[dt.getName()] = dt

def _resolve_type(type_str):
	key = type_str
	if key in _type_cache:
		return _type_cache[key]
	try:
		s = type_str.strip()
		if s.startswith('const '):
			s = s[6:].strip()
		ptrs = 0
		while s.endswith('*'):
			ptrs += 1
			s = s[:-1].strip()
		s = s.strip()
		if not s:
			_type_cache[key] = None
			return None
		t = _find_cache.get(s)
		if t is not None:
			for _ in range(ptrs):
				t = dtm.getPointer(t)
			_type_cache[key] = t
			return t
		try:
			st = StructureDataType(CategoryPath.ROOT, s, 1)
			t = dtm.resolve(st, handler)
			_find_cache[s] = t
			for _ in range(ptrs):
				t = dtm.getPointer(t)
			_type_cache[key] = t
			return t
		except:
			_type_cache[key] = None
			return None
	except:
		_type_cache[key] = None
		return None

skip_parse = 0
skip_resolve = 0
skip_exception = 0
sig_ok = 0
sig_skip = 0

def set_params(addr, sig):
	global skip_parse, skip_resolve, skip_exception, sig_ok, sig_skip
	func = getFunctionAt(addr)
	if func is None:
		sig_skip += 1
		return
	paren = sig.find('(')
	if paren == -1:
		sig_skip += 1
		return
	ps = sig[paren+1:].rsplit(')', 1)[0]
	pds = []
	if ps.strip() and ps.strip() != 'void':
		for p in [x.strip() for x in ps.split(',') if x.strip()]:
			ws = p.split()
			if len(ws) < 2:
				skip_parse += 1
				return
			pt = _resolve_type(' '.join(ws[:-1]))
			if pt is None or pt == _VOID:
				if pt is None:
					skip_resolve += 1
				return
			pds.append(ParameterDefinitionImpl(ws[-1], pt, None))
	if not pds:
		sig_skip += 1
		return
	seen = {}
	dedup_pds = []
	for pd in pds:
		n = pd.getName()
		if n in seen:
			seen[n] += 1
			dedup_pds.append(ParameterDefinitionImpl(n + str(seen[n]), pd.getDataType(), pd.getComment()))
		else:
			seen[n] = 1
			dedup_pds.append(pd)
	try:
		params = JArrayList()
		for pd in dedup_pds:
			params.add(ParameterImpl(pd.getName(), pd.getDataType(), currentProgram))
		func.replaceParameters(params, Function.FunctionUpdateType.DYNAMIC_STORAGE_ALL_PARAMS, False, SourceType.USER_DEFINED)
		sig_ok += 1
	except Exception as e:
		skip_exception += 1

print("Opening script.json...")
f = askFile("script.json from Il2cppdumper", "Open")
data = json.loads(open(f.absolutePath, 'r', encoding='utf-8').read())
methods = data.get("ScriptMethod", [])

total = len(methods)
print("Setting parameters for %d methods..." % total)
monitor.initialize(total)
monitor.setMessage("Parameters")

for i, sm in enumerate(methods):
	addr = baseAddress.add(sm["Address"])
	sig = sm["Signature"][:-1]
	set_params(addr, sig)
	monitor.incrementProgress(1)
	if monitor.isCancelled(): break
	if (i + 1) % 5000 == 0:
		print("  ... %d / %d" % (i + 1, total))

print("Done! OK=%d SKIP=%d parse=%d resolve=%d exception=%d" % (sig_ok, sig_skip, skip_parse, skip_resolve, skip_exception))
