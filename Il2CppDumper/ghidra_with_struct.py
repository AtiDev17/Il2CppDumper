# -*- coding: utf-8 -*-
import json
import sys
import traceback
from ghidra.program.model.symbol import SourceType
from ghidra.program.model.data import TypedefDataType, IntegerDataType, UnsignedIntegerDataType, LongLongDataType, UnsignedLongLongDataType, ShortDataType, UnsignedShortDataType, CharDataType, UnsignedCharDataType, DataTypeConflictHandler, CategoryPath, StructureDataType, FunctionDefinitionDataType, ParameterDefinitionImpl
from ghidra.program.model.listing import ParameterImpl, Function
from ghidra.program.model.util import CodeUnitInsertionException
from java.util import ArrayList as JArrayList

try:
	from ghidra.program.model.data import VoidDataType
	_VOID = VoidDataType.dataType
except:
	_VOID = None

processFields = [
	"ScriptMethod",
	"ScriptString",
	"ScriptMetadata",
	"ScriptMetadataMethod",
	"Addresses",
]

functionManager = currentProgram.getFunctionManager()
baseAddress = currentProgram.getImageBase()
USER_DEFINED = SourceType.USER_DEFINED

def register_typedefs():
	dtm = currentProgram.getDataTypeManager()
	typedefs = {
		"int8_t": CharDataType.dataType,
		"int16_t": ShortDataType.dataType,
		"int32_t": IntegerDataType.dataType,
		"int64_t": LongLongDataType.dataType,
		"uint8_t": UnsignedCharDataType.dataType,
		"uint16_t": UnsignedShortDataType.dataType,
		"uint32_t": UnsignedIntegerDataType.dataType,
		"uint64_t": UnsignedLongLongDataType.dataType,
		"size_t": UnsignedLongLongDataType.dataType,
		"intptr_t": LongLongDataType.dataType,
		"uintptr_t": UnsignedLongLongDataType.dataType,
		"bool": UnsignedCharDataType.dataType,
	}
	for name, baseType in typedefs.items():
		try:
			tdef = TypedefDataType(CategoryPath.ROOT, name, baseType)
			dtm.resolve(tdef, DataTypeConflictHandler.REPLACE_HANDLER)
		except:
			pass

register_typedefs()

_type_cache = {}
_find_cache = {}

def _find_dt(name):
	if name in _find_cache:
		return _find_cache[name]
	dtm = currentProgram.getDataTypeManager()
	dt_list = getDataTypes(name)
	if len(dt_list) > 0:
		_find_cache[name] = dt_list[0]
		return dt_list[0]
	try:
		st = StructureDataType(CategoryPath.ROOT, name, 1)
		dt = dtm.resolve(st, DataTypeConflictHandler.REPLACE_HANDLER)
		_find_cache[name] = dt
		return dt
	except:
		_find_cache[name] = None
		return None

def _resolve_type(dtm, type_str):
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
		if not s or s == 'void':
			if ptrs > 0:
				t = _VOID
				for _ in range(ptrs):
					t = dtm.getPointer(t)
				_type_cache[key] = t
				return t
			_type_cache[key] = _VOID
			return _VOID
		t = _find_dt(s)
		if t is None:
			_type_cache[key] = None
			return None
		for _ in range(ptrs):
			t = dtm.getPointer(t)
		_type_cache[key] = t
		return t
	except:
		_type_cache[key] = None
		return None

def import_needed_types(data):
	dtm = currentProgram.getDataTypeManager()
	handler = DataTypeConflictHandler.REPLACE_HANDLER
	needed = set()

	methods = data.get("ScriptMethod", [])
	monitor.initialize(len(methods))
	monitor.setMessage("Parsing signatures")
	for method in methods:
		sig = method.get("Signature", "")
		if not sig:
			monitor.incrementProgress(1)
			if monitor.isCancelled(): return
			continue
		sig = sig.rstrip(';')
		idx = sig.find('(')
		if idx == -1:
			monitor.incrementProgress(1)
			if monitor.isCancelled(): return
			continue
		sig_decl = sig[:idx]
		parts = sig_decl.split()
		if len(parts) > 1:
			rt = ' '.join(parts[:-1]).rstrip('*').strip()
			if rt and rt not in ('void', 'bool'):
				needed.add(rt)
		pm = sig[idx+1:].rsplit(')', 1)[0]
		for param in pm.split(','):
			param = param.strip()
			if not param or param == 'void':
				continue
			words = param.split()
			if len(words) < 2:
				continue
			if words[0] == 'const':
				if len(words) >= 3:
					pt = ' '.join(words[1:-1])
				else:
					continue
			else:
				pt = ' '.join(words[:-1])
			pt = pt.rstrip('*').strip()
			if pt and pt not in ('void', 'bool'):
				needed.add(pt)
		monitor.incrementProgress(1)
		if monitor.isCancelled(): return

	for meta in data.get("ScriptMetadata", []):
		sig = meta.get("Signature", "")
		if sig:
			clean = sig.rstrip('*').strip()
			if clean and clean not in ('void', 'bool'):
				needed.add(clean)

	print("Collected %d unique type names from %d methods" % (len(needed), len(methods)))

	monitor.setMessage("Building type cache")
	for dt in dtm.getAllDataTypes():
		_find_cache[dt.getName()] = dt
	print("Cached %d existing types" % len(_find_cache))

	monitor.setMessage("Creating type stubs")
	count = 0
	for name in sorted(needed, key=len, reverse=True):
		if name in _find_cache:
			continue
		dt_list = getDataTypes(name)
		if len(dt_list) > 0:
			_find_cache[name] = dt_list[0]
			continue
		try:
			st = StructureDataType(CategoryPath.ROOT, name, 1)
			dt = dtm.resolve(st, handler)
			_find_cache[name] = dt
			count += 1
		except:
			_find_cache[name] = None
		if count % 5000 == 0 and count > 0:
			print("  ... %d stubs created" % count)
	print("Imported %d type stubs" % count)

def get_addr(addr):
	return baseAddress.add(addr)

def set_name(addr, name):
	try:
		name = name.replace(' ', '-')
		createLabel(addr, name, True, USER_DEFINED)
	except:
		pass

def set_type(addr, type):
	dtm = currentProgram.getDataTypeManager()
	newType = type.replace("*"," *").replace("  "," ").strip()
	addrType = _find_dt(newType)
	if addrType is None and newType.endswith(" *"):
		baseName = newType[:-2]
		bt = _find_dt(baseName)
		if bt is not None:
			addrType = dtm.getPointer(bt)
	if addrType is not None:
		try:
			createData(addr, addrType)
		except CodeUnitInsertionException:
			pass

def make_function(start):
	func = getFunctionAt(start)
	if func is None:
		try:
			createFunction(start, None)
		except:
			pass

sig_ok = 0
sig_skip = 0
skip_no_paren = 0
skip_no_func = 0
skip_decl_parse = 0
skip_ret_type = 0
skip_param_parse = 0
skip_param_type = 0
skip_exception = 0

def set_sig(addr, name, sig):
	global sig_ok, sig_skip, skip_no_paren, skip_no_func, skip_decl_parse
	global skip_ret_type, skip_param_parse, skip_param_type, skip_exception
	try:
		sig = sig.rstrip(';')
		paren = sig.find('(')
		if paren == -1:
			skip_no_paren += 1
			sig_skip += 1
			if skip_no_paren <= 3:
				print("  [skip_no_paren] sig=%s" % sig)
			return

		func = getFunctionAt(addr)
		if func is None:
			skip_no_func += 1
			sig_skip += 1
			if skip_no_func <= 3:
				print("  [skip_no_func] addr=%s name=%s" % (addr, name))
			return

		dtm = currentProgram.getDataTypeManager()

		decl = sig[:paren].strip()
		dw = decl.split()
		if len(dw) < 2:
			skip_decl_parse += 1
			sig_skip += 1
			if skip_decl_parse <= 3:
				print("  [skip_decl_parse] decl=%s dw=%s" % (decl, dw))
			return

		rt = _resolve_type(dtm, ' '.join(dw[:-1]))
		if rt is None:
			skip_ret_type += 1
			sig_skip += 1
			if skip_ret_type <= 3:
				print("  [skip_ret_type] name=%s type_str='%s'" % (name, ' '.join(dw[:-1])))
			return

		ps = sig[paren+1:].rsplit(')', 1)[0]
		pds = []
		if ps.strip() and ps.strip() != 'void':
			for p in [x.strip() for x in ps.split(',') if x.strip()]:
				ws = p.split()
				if len(ws) < 2:
					skip_param_parse += 1
					sig_skip += 1
					if skip_param_parse <= 3:
						print("  [skip_param_parse] p='%s' ws=%s" % (p, ws))
					return
				pt = _resolve_type(dtm, ' '.join(ws[:-1]))
				if pt is None:
					skip_param_type += 1
					sig_skip += 1
					if skip_param_type <= 3:
						print("  [skip_param_type] name=%s param='%s'" % (name, ' '.join(ws[:-1])))
					return
				pds.append(ParameterDefinitionImpl(ws[-1], pt, None))

		if rt is not None:
			func.setReturnType(rt, SourceType.USER_DEFINED)
		sig_ok += 1
	except:
		skip_exception += 1
		sig_skip += 1
		if skip_exception <= 3:
			et, ev, tb = sys.exc_info()
			print("  [exception] name=%s sig=%s err_type=%s err_val=%s" % (name, sig, et, ev))
			traceback.print_exception(et, ev, tb)

print("Opening file dialog...")
f = askFile("script.json from Il2cppdumper", "Open")
print("File selected: %s" % f.absolutePath)
data = json.loads(open(f.absolutePath, 'r', encoding='utf-8').read())
print("Loaded script.json with %d methods" % len(data.get("ScriptMethod", [])))

print("Importing needed types...")
import_needed_types(data)
print("Import done.")

if "ScriptMethod" in data and "ScriptMethod" in processFields:
	scriptMethods = data["ScriptMethod"]
	monitor.initialize(len(scriptMethods))
	monitor.setMessage("Methods")
	for scriptMethod in scriptMethods:
		addr = get_addr(scriptMethod["Address"])
		name = scriptMethod["Name"]
		set_name(addr, name)
		monitor.incrementProgress(1)
		if monitor.isCancelled(): break

if "ScriptString" in data and "ScriptString" in processFields:
	index = 1
	scriptStrings = data["ScriptString"]
	monitor.initialize(len(scriptStrings))
	monitor.setMessage("Strings")
	for scriptString in scriptStrings:
		addr = get_addr(scriptString["Address"])
		value = scriptString["Value"]
		name = "StringLiteral_" + str(index)
		createLabel(addr, name, True, USER_DEFINED)
		setEOLComment(addr, value)
		index += 1
		monitor.incrementProgress(1)
		if monitor.isCancelled(): break

if "ScriptMetadata" in data and "ScriptMetadata" in processFields:
	scriptMetadatas = data["ScriptMetadata"]
	monitor.initialize(len(scriptMetadatas))
	monitor.setMessage("Metadata")
	for scriptMetadata in scriptMetadatas:
		addr = get_addr(scriptMetadata["Address"])
		name = scriptMetadata["Name"]
		set_name(addr, name)
		setEOLComment(addr, name)
		if scriptMetadata["Signature"]:
			set_type(addr, scriptMetadata["Signature"])
		monitor.incrementProgress(1)
		if monitor.isCancelled(): break

if "ScriptMetadataMethod" in data and "ScriptMetadataMethod" in processFields:
	scriptMetadataMethods = data["ScriptMetadataMethod"]
	monitor.initialize(len(scriptMetadataMethods))
	monitor.setMessage("Metadata Methods")
	for scriptMetadataMethod in scriptMetadataMethods:
		addr = get_addr(scriptMetadataMethod["Address"])
		name = scriptMetadataMethod["Name"]
		methodAddr = get_addr(scriptMetadataMethod["MethodAddress"])
		set_name(addr, name)
		setEOLComment(addr, name)
		monitor.incrementProgress(1)
		if monitor.isCancelled(): break

if "Addresses" in data and "Addresses" in processFields:
	addresses = data["Addresses"]
	monitor.initialize(len(addresses))
	monitor.setMessage("Addresses")
	for address in addresses:
		start = get_addr(address)
		make_function(start)
		monitor.incrementProgress(1)
		if monitor.isCancelled(): break

if "ScriptMethod" in data and "ScriptMethod" in processFields:
	scriptMethods = data["ScriptMethod"]
	monitor.initialize(len(scriptMethods))
	monitor.setMessage("Signatures")
	for scriptMethod in scriptMethods:
		addr = get_addr(scriptMethod["Address"])
		sig = scriptMethod["Signature"][:-1]
		name = scriptMethod["Name"]
		set_sig(addr, name, sig)
		monitor.incrementProgress(1)
		if monitor.isCancelled(): break

print('Script finished! OK=%d SKIP=%d' % (sig_ok, sig_skip))
print('  skip breakdown: no_paren=%d no_func=%d decl_parse=%d ret_type=%d param_parse=%d param_type=%d exception=%d' % (
	skip_no_paren, skip_no_func, skip_decl_parse, skip_ret_type,
	skip_param_parse, skip_param_type, skip_exception))
