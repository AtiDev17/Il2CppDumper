using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Il2CppDumper
{
    internal sealed class DummyAssemblyGenerator
    {
        public List<AssemblyDef> Assemblies = [];

        private readonly Il2CppExecutor executor;
        private readonly Metadata metadata;
        private readonly Il2Cpp il2Cpp;
        private readonly Dictionary<Il2CppTypeDefinition, TypeDef> typeDefinitionDic = [];
        private readonly Dictionary<Il2CppGenericParameter, GenericParam> genericParameterDic = [];
        private readonly MethodDef attributeAttribute;
        private readonly Dictionary<int, FieldDef> fieldDefinitionDic = [];
        private readonly Dictionary<int, PropertyDef> propertyDefinitionDic = [];
        private readonly Dictionary<int, MethodDef> methodDefinitionDic = [];
        private readonly Dictionary<Il2CppGenericParameter, GenericVar> genericVarDic = [];
        private readonly Dictionary<Il2CppGenericParameter, GenericMVar> genericMVarDic = [];
        internal DummyAssemblyGenerator(Il2CppExecutor il2CppExecutor, Config config)
        {
            executor = il2CppExecutor;
            metadata = il2CppExecutor.metadata;
            il2Cpp = il2CppExecutor.il2Cpp;

            var dummyMod = ModuleDefMD.Load(new MemoryStream(Resource1.Il2CppDummyDll));
            var il2CppDummyDll = dummyMod.Assembly;
            Assemblies.Add(il2CppDummyDll);
            var dummyMD = dummyMod;
            var addressAttribute = dummyMD.Types.First(x => x.Name == "AddressAttribute").Methods[0];
            var fieldOffsetAttribute = dummyMD.Types.First(x => x.Name == "FieldOffsetAttribute").Methods[0];
            attributeAttribute = dummyMD.Types.First(x => x.Name == "AttributeAttribute").Methods[0];
            var metadataOffsetAttribute = dummyMD.Types.First(x => x.Name == "MetadataOffsetAttribute").Methods[0];
            var tokenAttribute = dummyMD.Types.First(x => x.Name == "TokenAttribute").Methods[0];

            FixConstants(dummyMod);

            var resolver = new MyAssemblyResolver();
            resolver.Register(il2CppDummyDll);

            var parameterDefinitionDic = new Dictionary<int, Parameter>();
            var eventDefinitionDic = new Dictionary<int, EventDef>();

            foreach (var imageDef in metadata.imageDefs)
            {
                var imageName = metadata.GetStringFromIndex(imageDef.nameIndex);
                var aname = metadata.assemblyDefs[imageDef.assemblyIndex].aname;
                var assemblyName = metadata.GetStringFromIndex(aname.nameIndex);
                Version vers;
                if (aname.build >= 0)
                {
                    vers = new Version(aname.major, aname.minor, aname.build, aname.revision);
                }
                else
                {
                    vers = new Version(3, 7, 1, 6);
                }
                var asmNameInfo = new AssemblyNameInfo { Name = assemblyName, Version = vers, Culture = UTF8String.Empty };
                var asmDef = new AssemblyDefUser(asmNameInfo);
                var modDef = new ModuleDefUser(imageName) { Kind = ModuleKind.Dll };
                asmDef.Modules.Add(modDef);
                Assemblies.Add(asmDef);
                modDef.Types.Clear();
                var typeEnd = (int)imageDef.typeStart + (int)imageDef.typeCount;
                for (var index = (int)imageDef.typeStart; index < typeEnd; ++index)
                {
                    var typeDef = metadata.typeDefs[index];
                    var namespaceName = metadata.GetStringFromIndex(typeDef.namespaceIndex);
                    var typeName = metadata.GetStringFromIndex(typeDef.nameIndex);
                    var typeDefinition = new TypeDefUser(namespaceName, typeName) { Attributes = (TypeAttributes)typeDef.flags };
                    typeDefinitionDic.Add(typeDef, typeDefinition);
                    if (typeDef.declaringTypeIndex == -1)
                    {
                        modDef.Types.Add(typeDefinition);
                    }
                }
            }
            foreach (var imageDef in metadata.imageDefs)
            {
                var typeEnd = (int)imageDef.typeStart + (int)imageDef.typeCount;
                for (var index = (int)imageDef.typeStart; index < typeEnd; ++index)
                {
                    var typeDef = metadata.typeDefs[index];
                    var typeDefinition = typeDefinitionDic[typeDef];

                    for (int i = 0; i < typeDef.nested_type_count; i++)
                    {
                        var nestedIndex = metadata.nestedTypeIndices[typeDef.nestedTypesStart + i];
                        var nestedTypeDef = metadata.typeDefs[nestedIndex];
                        var nestedTypeDefinition = typeDefinitionDic[nestedTypeDef];
                        typeDefinition.NestedTypes.Add(nestedTypeDefinition);
                    }
                }
            }
            foreach (var imageDef in metadata.imageDefs)
            {
                var typeEnd = (int)imageDef.typeStart + (int)imageDef.typeCount;
                for (var index = (int)imageDef.typeStart; index < typeEnd; ++index)
                {
                    var typeDef = metadata.typeDefs[index];
                    var typeDefinition = typeDefinitionDic[typeDef];
                    var module = typeDefinition.Module;

                    if (config.DummyDllAddToken)
                    {
                        var customTokenAttribute = new CustomAttribute(module.Import(tokenAttribute));
                        customTokenAttribute.NamedArguments.Add(new CANamedArgument(true, module.CorLibTypes.String, "Token", new CAArgument(module.CorLibTypes.String, $"0x{typeDef.token:X}")));
                        typeDefinition.CustomAttributes.Add(customTokenAttribute);
                    }

                    if (typeDef.genericContainerIndex >= 0)
                    {
                        var genericContainer = metadata.genericContainers[typeDef.genericContainerIndex];
                        for (int i = 0; i < genericContainer.type_argc; i++)
                        {
                            var genericParameterIndex = genericContainer.genericParameterStart + i;
                            var param = metadata.genericParameters[genericParameterIndex];
                            var genericParameter = CreateGenericParameterDef(param, module);
                            typeDefinition.GenericParameters.Add(genericParameter);
                            genericVarDic[param] = new GenericVar((ushort)i, typeDefinition);
                        }
                    }

                    if (typeDef.parentIndex >= 0)
                    {
                        var parentType = il2Cpp.types[typeDef.parentIndex];
                        var parentTypeRef = GetITypeDefOrRef(module, parentType);
                        typeDefinition.BaseType = parentTypeRef;
                    }

                    for (int i = 0; i < typeDef.interfaces_count; i++)
                    {
                        var interfaceType = il2Cpp.types[metadata.interfaceOffsetPairs[typeDef.interfacesStart + i].interfaceTypeIndex];
                        var interfaceTypeRef = GetITypeDefOrRef(module, interfaceType);
                        typeDefinition.Interfaces.Add(new InterfaceImplUser(interfaceTypeRef));
                    }
                }
            }
            foreach (var imageDef in metadata.imageDefs)
            {
                var imageName = metadata.GetStringFromIndex(imageDef.nameIndex);
                var typeEnd = imageDef.typeStart + imageDef.typeCount;
                for (int index = imageDef.typeStart; index < typeEnd; index++)
                {
                    var typeDef = metadata.typeDefs[index];
                    var typeDefinition = typeDefinitionDic[typeDef];
                    var module = typeDefinition.Module;

                    var fieldEnd = typeDef.fieldStart + typeDef.field_count;
                    for (var i = typeDef.fieldStart; i < fieldEnd; ++i)
                    {
                        var fieldDef = metadata.fieldDefs[i];
                        var fieldType = il2Cpp.types[fieldDef.typeIndex];
                        var fieldName = metadata.GetStringFromIndex(fieldDef.nameIndex);
                        var fieldTypeSig = GetTypeSig(module, fieldType);
                        var fieldDefinition = new FieldDefUser(fieldName, new FieldSig(fieldTypeSig), (FieldAttributes)fieldType.attrs);
                        typeDefinition.Fields.Add(fieldDefinition);
                        fieldDefinitionDic.Add(i, fieldDefinition);

                        if (config.DummyDllAddToken)
                        {
                            var customTokenAttribute = new CustomAttribute(module.Import(tokenAttribute));
                            customTokenAttribute.NamedArguments.Add(new CANamedArgument(true, module.CorLibTypes.String, "Token", new CAArgument(module.CorLibTypes.String, $"0x{fieldDef.token:X}")));
                            fieldDefinition.CustomAttributes.Add(customTokenAttribute);
                        }

                        if (metadata.GetFieldDefaultValueFromIndex(i, out var fieldDefault) && fieldDefault.dataIndex != -1)
                        {
                            if (executor.TryGetDefaultValue(fieldDefault.typeIndex, fieldDefault.dataIndex, out var value))
                            {
                                fieldDefinition.Constant = ToConstant(value);
                            }
                            else if (config.DummyDllAddOffset)
                            {
                                var customAttribute = new CustomAttribute(module.Import(metadataOffsetAttribute));
                                var offset = new CANamedArgument(true, module.CorLibTypes.String, "Offset", new CAArgument(module.CorLibTypes.String, $"0x{value:X}"));
                                customAttribute.NamedArguments.Add(offset);
                                fieldDefinition.CustomAttributes.Add(customAttribute);
                            }
                        }
                        if (!fieldDefinition.IsLiteral && config.DummyDllAddOffset)
                        {
                            var fieldOffset = il2Cpp.GetFieldOffsetFromIndex(index, i - typeDef.fieldStart, i, typeDefinition.IsValueType, fieldDefinition.IsStatic);
                            if (fieldOffset >= 0)
                            {
                                var customAttribute = new CustomAttribute(module.Import(fieldOffsetAttribute));
                                var offset = new CANamedArgument(true, module.CorLibTypes.String, "Offset", new CAArgument(module.CorLibTypes.String, $"0x{fieldOffset:X}"));
                                customAttribute.NamedArguments.Add(offset);
                                fieldDefinition.CustomAttributes.Add(customAttribute);
                            }
                        }
                    }
                    var methodEnd = typeDef.methodStart + typeDef.method_count;
                    for (var i = typeDef.methodStart; i < methodEnd; ++i)
                    {
                        var methodDef = metadata.methodDefs[i];
                        var methodName = metadata.GetStringFromIndex(methodDef.nameIndex);
                        var returnTypeSig = GetTypeSig(module, il2Cpp.types[methodDef.returnType]);
                        var hasThis = (methodDef.flags & 0x20) == 0;
                        var methodSig = new MethodSig(hasThis ? CallingConvention.HasThis : CallingConvention.Default) { RetType = returnTypeSig };
                        var methodDefinition = new MethodDefUser(methodName, methodSig, (MethodImplAttributes)methodDef.iflags, (MethodAttributes)methodDef.flags);
                        typeDefinition.Methods.Add(methodDefinition);

                        if (methodDef.genericContainerIndex >= 0)
                        {
                            var genericContainer = metadata.genericContainers[methodDef.genericContainerIndex];
                            for (int j = 0; j < genericContainer.type_argc; j++)
                            {
                                var genericParameterIndex = genericContainer.genericParameterStart + j;
                                var param = metadata.genericParameters[genericParameterIndex];
                                var genericParameter = CreateGenericParameterDef(param, module);
                                methodDefinition.GenericParameters.Add(genericParameter);
                                genericMVarDic[param] = new GenericMVar((ushort)j, methodDefinition);
                            }
                        }

                        if (config.DummyDllAddToken)
                        {
                            var customTokenAttribute = new CustomAttribute(module.Import(tokenAttribute));
                            customTokenAttribute.NamedArguments.Add(new CANamedArgument(true, module.CorLibTypes.String, "Token", new CAArgument(module.CorLibTypes.String, $"0x{methodDef.token:X}")));
                            methodDefinition.CustomAttributes.Add(customTokenAttribute);
                        }

                        if (methodDefinition.HasBody && typeDefinition.BaseType?.FullName != "System.MulticastDelegate")
                        {
                            var il = methodDefinition.Body.Instructions;
                            if (returnTypeSig.RemoveModifiers().GetElementType() == ElementType.Void)
                            {
                                il.Add(Instruction.Create(OpCodes.Ret));
                            }
                            else if (returnTypeSig.IsValueType)
                            {
                                var variable = new Local(returnTypeSig);
                                _ = methodDefinition.Body.Variables.Add(variable);
                                il.Add(Instruction.Create(OpCodes.Ldloca_S, variable));
                                il.Add(Instruction.Create(OpCodes.Initobj, new TypeSpecUser(returnTypeSig)));
                                il.Add(Instruction.Create(OpCodes.Ldloc_0));
                                il.Add(Instruction.Create(OpCodes.Ret));
                            }
                            else
                            {
                                il.Add(Instruction.Create(OpCodes.Ldnull));
                                il.Add(Instruction.Create(OpCodes.Ret));
                            }
                        }
                        methodDefinitionDic.Add(i, methodDefinition);

                        for (var j = 0; j < methodDef.parameterCount; ++j)
                        {
                            var parameterDef = metadata.parameterDefs[methodDef.parameterStart + j];
                            var parameterName = metadata.GetStringFromIndex(parameterDef.nameIndex);
                            var parameterType = il2Cpp.types[parameterDef.typeIndex];
                            var paramTypeSig = GetTypeSigWithByRef(module, parameterType);
                            methodSig.Params.Add(paramTypeSig);
                            var paramDef = new ParamDefUser(parameterName, (ushort)j) { Attributes = (ParamAttributes)parameterType.attrs };
                            methodDefinition.ParamDefs.Add(paramDef);
                            methodDefinition.Parameters.UpdateParameterTypes();
                            parameterDefinitionDic.Add(methodDef.parameterStart + j, methodDefinition.Parameters[j]);

                            if (metadata.GetParameterDefaultValueFromIndex(methodDef.parameterStart + j, out var parameterDefault) && parameterDefault.dataIndex != -1)
                            {
                                if (executor.TryGetDefaultValue(parameterDefault.typeIndex, parameterDefault.dataIndex, out var value))
                                {
                                    paramDef.Constant = ToConstant(value);
                                }
                                else if (config.DummyDllAddOffset)
                                {
                                    var customAttribute = new CustomAttribute(module.Import(metadataOffsetAttribute));
                                    var offset = new CANamedArgument(true, module.CorLibTypes.String, "Offset", new CAArgument(module.CorLibTypes.String, $"0x{value:X}"));
                                    customAttribute.NamedArguments.Add(offset);
                                    paramDef.CustomAttributes.Add(customAttribute);
                                }
                            }
                        }
                        methodDefinition.Parameters.UpdateParameterTypes();
                        if (!methodDefinition.IsAbstract && config.DummyDllAddOffset)
                        {
                            var methodPointer = il2Cpp.GetMethodPointer(imageName, methodDef);
                            if (methodPointer > 0)
                            {
                                var customAttribute = new CustomAttribute(module.Import(addressAttribute));
                                var fixedMethodPointer = il2Cpp.GetRVA(methodPointer);
                                customAttribute.NamedArguments.Add(new CANamedArgument(true, module.CorLibTypes.String, "RVA", new CAArgument(module.CorLibTypes.String, $"0x{fixedMethodPointer:X}")));
                                customAttribute.NamedArguments.Add(new CANamedArgument(true, module.CorLibTypes.String, "Offset", new CAArgument(module.CorLibTypes.String, $"0x{il2Cpp.MapVATR(methodPointer):X}")));
                                customAttribute.NamedArguments.Add(new CANamedArgument(true, module.CorLibTypes.String, "VA", new CAArgument(module.CorLibTypes.String, $"0x{methodPointer:X}")));
                                if (methodDef.slot != ushort.MaxValue)
                                {
                                    customAttribute.NamedArguments.Add(new CANamedArgument(true, module.CorLibTypes.String, "Slot", new CAArgument(module.CorLibTypes.String, methodDef.slot.ToString(CultureInfo.InvariantCulture))));
                                }
                                methodDefinition.CustomAttributes.Add(customAttribute);
                            }
                        }
                    }
                    var propertyEnd = typeDef.propertyStart + typeDef.property_count;
                    for (var i = typeDef.propertyStart; i < propertyEnd; ++i)
                    {
                        var propertyDef = metadata.propertyDefs[i];
                        var propertyName = metadata.GetStringFromIndex(propertyDef.nameIndex);
                        TypeSig propertyType = null;
                        MethodDef GetMethod = null;
                        MethodDef SetMethod = null;
                        if (propertyDef.get >= 0)
                        {
                            if (!methodDefinitionDic.TryGetValue(typeDef.methodStart + propertyDef.get, out GetMethod))
                                continue;
                            propertyType = GetMethod.MethodSig.RetType;
                        }
                        if (propertyDef.set >= 0)
                        {
                            if (!methodDefinitionDic.TryGetValue(typeDef.methodStart + propertyDef.set, out SetMethod))
                                continue;
                            if (SetMethod.MethodSig.Params.Count > 0)
                                propertyType ??= SetMethod.MethodSig.Params[0];
                        }
                        propertyType ??= module.CorLibTypes.Object;
                        var propertySig = new PropertySig(false, propertyType);
                        var propertyDefinition = new PropertyDefUser(propertyName, propertySig, (PropertyAttributes)propertyDef.attrs)
                        {
                            GetMethod = GetMethod,
                            SetMethod = SetMethod
                        };
                        typeDefinition.Properties.Add(propertyDefinition);
                        propertyDefinitionDic.Add(i, propertyDefinition);

                        if (config.DummyDllAddToken)
                        {
                            var customTokenAttribute = new CustomAttribute(module.Import(tokenAttribute));
                            customTokenAttribute.NamedArguments.Add(new CANamedArgument(true, module.CorLibTypes.String, "Token", new CAArgument(module.CorLibTypes.String, $"0x{propertyDef.token:X}")));
                            propertyDefinition.CustomAttributes.Add(customTokenAttribute);
                        }
                    }
                    var eventEnd = typeDef.eventStart + typeDef.event_count;
                    for (var i = typeDef.eventStart; i < eventEnd; ++i)
                    {
                        var eventDef = metadata.eventDefs[i];
                        var eventName = metadata.GetStringFromIndex(eventDef.nameIndex);
                        var eventType = il2Cpp.types[eventDef.typeIndex];
                        var eventTypeRef = GetITypeDefOrRef(module, eventType);
                        var eventDefinition = new EventDefUser(eventName, eventTypeRef) { Attributes = (EventAttributes)eventType.attrs };
                        if (eventDef.add >= 0)
                            eventDefinition.AddMethod = methodDefinitionDic[typeDef.methodStart + eventDef.add];
                        if (eventDef.remove >= 0)
                            eventDefinition.RemoveMethod = methodDefinitionDic[typeDef.methodStart + eventDef.remove];
                        if (eventDef.raise >= 0)
                            eventDefinition.InvokeMethod = methodDefinitionDic[typeDef.methodStart + eventDef.raise];
                        typeDefinition.Events.Add(eventDefinition);
                        eventDefinitionDic.Add(i, eventDefinition);

                        if (config.DummyDllAddToken)
                        {
                            var customTokenAttribute = new CustomAttribute(module.Import(tokenAttribute));
                            customTokenAttribute.NamedArguments.Add(new CANamedArgument(true, module.CorLibTypes.String, "Token", new CAArgument(module.CorLibTypes.String, $"0x{eventDef.token:X}")));
                            eventDefinition.CustomAttributes.Add(customTokenAttribute);
                        }
                    }
                }
            }
            if (il2Cpp.Version > 20)
            {
                foreach (var imageDef in metadata.imageDefs)
                {
                    var typeEnd = imageDef.typeStart + imageDef.typeCount;
                    for (int index = imageDef.typeStart; index < typeEnd; index++)
                    {
                        var typeDef = metadata.typeDefs[index];
                        var typeDefinition = typeDefinitionDic[typeDef];
                        var module = typeDefinition.Module;

                        CreateCustomAttribute(imageDef, typeDef.customAttributeIndex, typeDef.token, module, typeDefinition.CustomAttributes);

                        var fieldEnd = typeDef.fieldStart + typeDef.field_count;
                        for (var i = typeDef.fieldStart; i < fieldEnd; ++i)
                        {
                            var fieldDef = metadata.fieldDefs[i];
                            var fieldDefinition = fieldDefinitionDic[i];
                            CreateCustomAttribute(imageDef, fieldDef.customAttributeIndex, fieldDef.token, module, fieldDefinition.CustomAttributes);
                        }

                        var methodEnd = typeDef.methodStart + typeDef.method_count;
                        for (var i = typeDef.methodStart; i < methodEnd; ++i)
                        {
                            var methodDef = metadata.methodDefs[i];
                            var methodDefinition = methodDefinitionDic[i];
                            CreateCustomAttribute(imageDef, methodDef.customAttributeIndex, methodDef.token, module, methodDefinition.CustomAttributes);

                            for (var j = 0; j < methodDef.parameterCount; ++j)
                            {
                                var paramDef = metadata.parameterDefs[methodDef.parameterStart + j];
                                var parameter = methodDefinition.Parameters[j];
                                var paramDefObj = parameter.ParamDef;
                                if (paramDefObj != null)
                                {
                                    CreateCustomAttribute(imageDef, paramDef.customAttributeIndex, paramDef.token, module, paramDefObj.CustomAttributes);
                                }
                            }
                        }

                        var propertyEnd = typeDef.propertyStart + typeDef.property_count;
                        for (var i = typeDef.propertyStart; i < propertyEnd; ++i)
                        {
                            var propertyDef = metadata.propertyDefs[i];
                            var propertyDefinition = propertyDefinitionDic[i];
                            CreateCustomAttribute(imageDef, propertyDef.customAttributeIndex, propertyDef.token, module, propertyDefinition.CustomAttributes);
                        }

                        var eventEnd = typeDef.eventStart + typeDef.event_count;
                        for (var i = typeDef.eventStart; i < eventEnd; ++i)
                        {
                            var eventDef = metadata.eventDefs[i];
                            var eventDefinition = eventDefinitionDic[i];
                            CreateCustomAttribute(imageDef, eventDef.customAttributeIndex, eventDef.token, module, eventDefinition.CustomAttributes);
                        }
                    }
                }
            }
        }

        private TypeSig GetTypeSigWithByRef(ModuleDef module, Il2CppType il2CppType)
        {
            var typeSig = GetTypeSig(module, il2CppType);
            if (il2CppType.byref == 1)
            {
                return new ByRefSig(typeSig);
            }
            else
            {
                return typeSig;
            }
        }

        private TypeSig GetTypeSig(ModuleDef module, Il2CppType il2CppType)
        {
            switch (il2CppType.type)
            {
                case Il2CppTypeEnum.IL2CPP_TYPE_OBJECT:
                    return module.CorLibTypes.Object;
                case Il2CppTypeEnum.IL2CPP_TYPE_VOID:
                    return module.CorLibTypes.Void;
                case Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                    return module.CorLibTypes.Boolean;
                case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                    return module.CorLibTypes.Char;
                case Il2CppTypeEnum.IL2CPP_TYPE_I1:
                    return module.CorLibTypes.SByte;
                case Il2CppTypeEnum.IL2CPP_TYPE_U1:
                    return module.CorLibTypes.Byte;
                case Il2CppTypeEnum.IL2CPP_TYPE_I2:
                    return module.CorLibTypes.Int16;
                case Il2CppTypeEnum.IL2CPP_TYPE_U2:
                    return module.CorLibTypes.UInt16;
                case Il2CppTypeEnum.IL2CPP_TYPE_I4:
                    return module.CorLibTypes.Int32;
                case Il2CppTypeEnum.IL2CPP_TYPE_U4:
                    return module.CorLibTypes.UInt32;
                case Il2CppTypeEnum.IL2CPP_TYPE_I:
                    return module.CorLibTypes.IntPtr;
                case Il2CppTypeEnum.IL2CPP_TYPE_U:
                    return module.CorLibTypes.UIntPtr;
                case Il2CppTypeEnum.IL2CPP_TYPE_I8:
                    return module.CorLibTypes.Int64;
                case Il2CppTypeEnum.IL2CPP_TYPE_U8:
                    return module.CorLibTypes.UInt64;
                case Il2CppTypeEnum.IL2CPP_TYPE_R4:
                    return module.CorLibTypes.Single;
                case Il2CppTypeEnum.IL2CPP_TYPE_R8:
                    return module.CorLibTypes.Double;
                case Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                    return module.CorLibTypes.String;
                case Il2CppTypeEnum.IL2CPP_TYPE_TYPEDBYREF:
                    return module.CorLibTypes.TypedReference;
                case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                    {
                        var typeDef = executor.GetTypeDefinitionFromIl2CppType(il2CppType);
                        return new ClassSig(GetTypeDefOrRef(module, typeDef));
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                    {
                        var typeDef = executor.GetTypeDefinitionFromIl2CppType(il2CppType);
                        return new ValueTypeSig(GetTypeDefOrRef(module, typeDef));
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
                    {
                        var arrayType = il2Cpp.MapVATR<Il2CppArrayType>(il2CppType.data.array);
                        var oriType = il2Cpp.GetIl2CppType(arrayType.etype);
                        return new ArraySig(GetTypeSig(module, oriType), arrayType.rank);
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
                    {
                        var genericClass = il2Cpp.MapVATR<Il2CppGenericClass>(il2CppType.data.generic_class);
                        var typeDef = executor.GetGenericClassTypeDefinition(genericClass);
                        var typeDefOrRef = GetTypeDefOrRef(module, typeDef);
                        var genericInst = il2Cpp.MapVATR<Il2CppGenericInst>(genericClass.context.class_inst);
                        var pointers = il2Cpp.MapVATR<ulong>(genericInst.type_argv, genericInst.type_argc);
                        var genArgs = new List<TypeSig>();
                        foreach (var pointer in pointers)
                        {
                            var oriType = il2Cpp.GetIl2CppType(pointer);
                            genArgs.Add(GetTypeSig(module, oriType));
                        }
                        return new GenericInstSig(typeDef.IsValueType ? new ValueTypeSig(typeDefOrRef) : new ClassSig(typeDefOrRef), genArgs);
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                    {
                        var oriType = il2Cpp.GetIl2CppType(il2CppType.data.type);
                        return new SZArraySig(GetTypeSig(module, oriType));
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
                    {
                        var gp = executor.GetGenericParameteFromIl2CppType(il2CppType);
                        if (genericVarDic.TryGetValue(gp, out var gv))
                            return gv;
                        return new GenericVar(gp.num);
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
                    {
                        var gp = executor.GetGenericParameteFromIl2CppType(il2CppType);
                        if (genericMVarDic.TryGetValue(gp, out var gmv))
                            return gmv;
                        return new GenericMVar(gp.num);
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
                    {
                        var oriType = il2Cpp.GetIl2CppType(il2CppType.data.type);
                        return new PtrSig(GetTypeSig(module, oriType));
                    }
                default:
                    throw new NotSupportedException();
            }
        }

        private ITypeDefOrRef GetTypeDefOrRef(ModuleDef module, Il2CppTypeDefinition typeDef)
        {
            var td = typeDefinitionDic[typeDef];
            if (td.Module == module)
                return td;
            return module.Import(td);
        }

        private ITypeDefOrRef GetITypeDefOrRef(ModuleDef module, Il2CppType il2CppType)
        {
            switch (il2CppType.type)
            {
                case Il2CppTypeEnum.IL2CPP_TYPE_OBJECT:
                    return module.Import(typeof(object));
                case Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                    return module.Import(typeof(bool));
                case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                    return module.Import(typeof(char));
                case Il2CppTypeEnum.IL2CPP_TYPE_I1:
                    return module.Import(typeof(sbyte));
                case Il2CppTypeEnum.IL2CPP_TYPE_U1:
                    return module.Import(typeof(byte));
                case Il2CppTypeEnum.IL2CPP_TYPE_I2:
                    return module.Import(typeof(short));
                case Il2CppTypeEnum.IL2CPP_TYPE_U2:
                    return module.Import(typeof(ushort));
                case Il2CppTypeEnum.IL2CPP_TYPE_I4:
                    return module.Import(typeof(int));
                case Il2CppTypeEnum.IL2CPP_TYPE_U4:
                    return module.Import(typeof(uint));
                case Il2CppTypeEnum.IL2CPP_TYPE_I:
                    return module.Import(typeof(IntPtr));
                case Il2CppTypeEnum.IL2CPP_TYPE_U:
                    return module.Import(typeof(UIntPtr));
                case Il2CppTypeEnum.IL2CPP_TYPE_I8:
                    return module.Import(typeof(long));
                case Il2CppTypeEnum.IL2CPP_TYPE_U8:
                    return module.Import(typeof(ulong));
                case Il2CppTypeEnum.IL2CPP_TYPE_R4:
                    return module.Import(typeof(float));
                case Il2CppTypeEnum.IL2CPP_TYPE_R8:
                    return module.Import(typeof(double));
                case Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                    return module.Import(typeof(string));
                case Il2CppTypeEnum.IL2CPP_TYPE_TYPEDBYREF:
                    return module.Import(typeof(TypedReference));
                case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                    {
                        var typeDef = executor.GetTypeDefinitionFromIl2CppType(il2CppType);
                        return GetTypeDefOrRef(module, typeDef);
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
                    {
                        var genericClass = il2Cpp.MapVATR<Il2CppGenericClass>(il2CppType.data.generic_class);
                        var typeDef = executor.GetGenericClassTypeDefinition(genericClass);
                        var typeDefOrRef = GetTypeDefOrRef(module, typeDef);
                        var genericInst = il2Cpp.MapVATR<Il2CppGenericInst>(genericClass.context.class_inst);
                        var pointers = il2Cpp.MapVATR<ulong>(genericInst.type_argv, genericInst.type_argc);
                        var genArgs = new List<TypeSig>();
                        foreach (var pointer in pointers)
                        {
                            var oriType = il2Cpp.GetIl2CppType(pointer);
                            genArgs.Add(GetTypeSig(module, oriType));
                        }
                        return new TypeSpecUser(new GenericInstSig(typeDef.IsValueType ? new ValueTypeSig(typeDefOrRef) : new ClassSig(typeDefOrRef), genArgs));
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                    {
                        var oriType = il2Cpp.GetIl2CppType(il2CppType.data.type);
                        return new TypeSpecUser(new SZArraySig(GetTypeSig(module, oriType)));
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
                    {
                        var gp = executor.GetGenericParameteFromIl2CppType(il2CppType);
                        if (genericVarDic.TryGetValue(gp, out var gv))
                            return new TypeSpecUser(gv);
                        return new TypeSpecUser(new GenericVar(gp.num));
                    }
                case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
                    {
                        var gp = executor.GetGenericParameteFromIl2CppType(il2CppType);
                        if (genericMVarDic.TryGetValue(gp, out var gmv))
                            return new TypeSpecUser(gmv);
                        return new TypeSpecUser(new GenericMVar(gp.num));
                    }
                default:
                    throw new NotSupportedException();
            }
        }

        private void CreateCustomAttribute(Il2CppImageDefinition imageDef, int customAttributeIndex, uint token, ModuleDef moduleDef, CustomAttributeCollection customAttributes)
        {
            var attributeIndex = metadata.GetCustomAttributeIndex(imageDef, customAttributeIndex, token);
            if (attributeIndex >= 0)
            {
                try
                {
                    if (il2Cpp.Version < 29)
                    {
                        var attributeTypeRange = metadata.attributeTypeRanges[attributeIndex];
                        for (int i = 0; i < attributeTypeRange.count; i++)
                        {
                            var attributeTypeIndex = metadata.attributeTypes[attributeTypeRange.start + i];
                            var attributeType = il2Cpp.types[attributeTypeIndex];
                            var typeDef = executor.GetTypeDefinitionFromIl2CppType(attributeType);
                            var typeDefinition = typeDefinitionDic[typeDef];
                            if (!TryRestoreCustomAttribute(typeDefinition, moduleDef, customAttributes))
                            {
                                var methodPointer = executor.customAttributeGenerators[attributeIndex];
                                var fixedMethodPointer = il2Cpp.GetRVA(methodPointer);
                                var customAttribute = new CustomAttribute(moduleDef.Import(attributeAttribute));
                                customAttribute.NamedArguments.Add(new CANamedArgument(true, moduleDef.CorLibTypes.String, "Name", new CAArgument(moduleDef.CorLibTypes.String, (string)typeDefinition.Name)));
                                customAttribute.NamedArguments.Add(new CANamedArgument(true, moduleDef.CorLibTypes.String, "RVA", new CAArgument(moduleDef.CorLibTypes.String, $"0x{fixedMethodPointer:X}")));
                                customAttribute.NamedArguments.Add(new CANamedArgument(true, moduleDef.CorLibTypes.String, "Offset", new CAArgument(moduleDef.CorLibTypes.String, $"0x{il2Cpp.MapVATR(methodPointer):X}")));
                                customAttributes.Add(customAttribute);
                            }
                        }
                    }
                    else
                    {
                        var startRange = metadata.attributeDataRanges[attributeIndex];
                        var endRange = metadata.attributeDataRanges[attributeIndex + 1];
                        metadata.Position = (il2Cpp.Version < 38 ? metadata.header.attributeDataOffset : metadata.header.attributeData.offset) + startRange.startOffset;
                        var buff = metadata.ReadBytes((int)(endRange.startOffset - startRange.startOffset));
                        using var reader = new CustomAttributeDataReader(executor, buff);
                        if (reader.Count != 0)
                        {
                            for (var i = 0; i < reader.Count; i++)
                            {
                                var visitor = reader.VisitCustomAttributeData();
                                if (visitor == null) continue;
                                if (!methodDefinitionDic.TryGetValue(visitor.CtorIndex, out var methodDefinition)) continue;
                                var customAttribute = new CustomAttribute(moduleDef.Import(methodDefinition));
                                foreach (var argument in visitor.Arguments)
                                {
                                    if (argument.Index >= methodDefinition.MethodSig.Params.Count) continue;
                                    var paramType = methodDefinition.MethodSig.Params[argument.Index];
                                    var customAttributeArgument = CreateCustomAttributeArgument(paramType, argument.Value, moduleDef);
                                    customAttribute.ConstructorArguments.Add(customAttributeArgument);
                                }
                                foreach (var field in visitor.Fields)
                                {
                                    if (!fieldDefinitionDic.TryGetValue(field.Index, out var fieldDefinition)) continue;
                                    if (fieldDefinition.FieldType == null) continue;
                                    var customAttributeArgument = CreateCustomAttributeArgument(fieldDefinition.FieldType, field.Value, moduleDef);
                                    customAttribute.NamedArguments.Add(new CANamedArgument(true, fieldDefinition.FieldType, fieldDefinition.Name, customAttributeArgument));
                                }
                                foreach (var property in visitor.Properties)
                                {
                                    if (!propertyDefinitionDic.TryGetValue(property.Index, out var propertyDefinition)) continue;
                                    if (propertyDefinition.PropertySig == null) continue;
                                    var propType = propertyDefinition.PropertySig.RetType;
                                    if (propType == null) continue;
                                    var customAttributeArgument = CreateCustomAttributeArgument(propType, property.Value, moduleDef);
                                    customAttribute.NamedArguments.Add(new CANamedArgument(false, propType, propertyDefinition.Name, customAttributeArgument));
                                }
                                customAttributes.Add(customAttribute);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Error while restoring attributeIndex {attributeIndex}: {ex.Message}");
                }
            }
        }

        private static bool TryRestoreCustomAttribute(TypeDef attributeType, ModuleDef moduleDef, CustomAttributeCollection customAttributes)
        {
            if (attributeType.Methods.Count == 1 && attributeType.Name != "CompilerGeneratedAttribute")
            {
                var methodDefinition = attributeType.Methods[0];
                if (methodDefinition.Name == ".ctor" && methodDefinition.Parameters.Count == 0)
                {
                    var customAttribute = new CustomAttribute(moduleDef.Import(methodDefinition));
                    customAttributes.Add(customAttribute);
                    return true;
                }
            }
            return false;
        }

        private static ConstantUser? ToConstant(object value)
        {
            if (value == null)
                return null;
            if (value is bool b)
                return new ConstantUser { Value = b ? 1 : 0, Type = ElementType.I4 };
            if (value is char c)
                return new ConstantUser { Value = (ushort)c, Type = ElementType.U2 };
            if (value is sbyte sb)
                return new ConstantUser { Value = sb, Type = ElementType.I1 };
            if (value is byte ub)
                return new ConstantUser { Value = ub, Type = ElementType.U1 };
            if (value is short s)
                return new ConstantUser { Value = s, Type = ElementType.I2 };
            if (value is ushort us)
                return new ConstantUser { Value = us, Type = ElementType.U2 };
            if (value is int i)
                return new ConstantUser { Value = i, Type = ElementType.I4 };
            if (value is uint ui)
                return new ConstantUser { Value = ui, Type = ElementType.U4 };
            if (value is long l)
                return new ConstantUser { Value = l, Type = ElementType.I8 };
            if (value is ulong ul)
                return new ConstantUser { Value = ul, Type = ElementType.U8 };
            if (value is float f)
                return new ConstantUser { Value = f, Type = ElementType.R4 };
            if (value is double d)
                return new ConstantUser { Value = d, Type = ElementType.R8 };
            if (value is string str)
                return new ConstantUser { Value = str, Type = ElementType.String };
            return null;
        }

        private static void FixConstants(ModuleDef module)
        {
            foreach (var type in module.GetTypes())
            {
                foreach (var field in type.Fields)
                    field.Constant = null;
                foreach (var prop in type.Properties)
                    prop.Constant = null;
                foreach (var method in type.Methods)
                {
                    foreach (var param in method.ParamDefs)
                        param.Constant = null;
                }
            }
        }

        private GenericParam CreateGenericParameterDef(Il2CppGenericParameter param, ModuleDef module)
        {
            if (!genericParameterDic.TryGetValue(param, out var genericParameter))
            {
                var genericName = metadata.GetStringFromIndex(param.nameIndex);
                genericParameter = new GenericParamUser(param.num, (GenericParamAttributes)param.flags, genericName);
                genericParameterDic.Add(param, genericParameter);
                for (int i = 0; i < param.constraintsCount; ++i)
                {
                    var il2CppType = il2Cpp.types[metadata.constraintIndices[param.constraintsStart + i].index];
                    genericParameter.GenericParamConstraints.Add(new GenericParamConstraintUser(GetITypeDefOrRef(module, il2CppType)));
                }
            }
            return genericParameter;
        }

        private CAArgument CreateCustomAttributeArgument(TypeSig typeSig, BlobValue blobValue, ModuleDef moduleDef)
        {
            if (blobValue == null) return new CAArgument(typeSig);
            var val = blobValue.Value;
            if (typeSig.RemoveModifiers().GetElementType() == ElementType.Object)
            {
                if (blobValue.il2CppTypeEnum == Il2CppTypeEnum.IL2CPP_TYPE_IL2CPP_TYPE_INDEX)
                {
                    val = new CAArgument(moduleDef.CorLibTypes.Object, GetTypeSig(moduleDef, (Il2CppType)val));
                }
                else
                {
                    val = new CAArgument(GetBlobValueTypeSig(blobValue, moduleDef), val);
                }
            }
            else if (val == null)
            {
                return new CAArgument(typeSig, val);
            }
            else if (typeSig.RemoveModifiers().GetElementType() == ElementType.SZArray ||
                     typeSig.RemoveModifiers().GetElementType() == ElementType.Array)
            {
                var arrayVal = (BlobValue[])val;
                var array = new CAArgument[arrayVal.Length];
                var elementType = typeSig.Next;
                for (int i = 0; i < arrayVal.Length; i++)
                {
                    array[i] = CreateCustomAttributeArgument(elementType, arrayVal[i], moduleDef);
                }
                val = array;
            }
            else if (typeSig.RemoveModifiers().FullName == "System.Type")
            {
                val = GetTypeSig(moduleDef, (Il2CppType)val);
            }
            return new CAArgument(typeSig, val);
        }

        private TypeSig GetBlobValueTypeSig(BlobValue blobValue, ModuleDef moduleDef)
        {
            if (blobValue.EnumType != null)
            {
                return GetTypeSig(moduleDef, blobValue.EnumType);
            }
            var il2CppType = new Il2CppType
            {
                type = blobValue.il2CppTypeEnum
            };
            return GetTypeSig(moduleDef, il2CppType);
        }
    }
}
