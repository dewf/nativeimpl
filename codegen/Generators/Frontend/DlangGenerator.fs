module Generators.Frontend.DlangGenerator

open System.IO
open Parser

open Util
open Generators.Common

let rec typeToDlangString (typeMap: Map<string, UserOrGeneric>) = function
    | UnitType -> "void"
    | Primitive prim ->
        match prim with
        | SizeT -> "size_t"
        | Bool -> "bool"
        | Int(signed, intWidth) ->
            match signed, intWidth with
            | true, Width8 -> "byte"
            | false, Width8 -> "ubyte"
            | true, Width16 -> "short"
            | false, Width16 -> "ushort"
            | true, Width32 -> "int"
            | false, Width32 -> "uint"
            | true, Width64 -> "long"
            | false, Width64 -> "ulong"
        | Float32 -> "float"
        | Float64 -> "double"
        | StringType -> "string"
    | OpaqueHandleType name -> upperFirst name
    | EnumType name -> upperFirst name
    | SumType name -> upperFirst name
    | StructType name -> upperFirst name
    | InterfaceType name -> upperFirst name
    | ExceptionType name -> upperFirst name
    | DelegateType name -> upperFirst name
    | ArrayType niType -> sprintf "%s[]" (typeToDlangString typeMap niType)
    | SetType niType -> sprintf "Set!%s" (typeToDlangString typeMap niType)
    // | BufferType elemType -> sprintf "INativeBuffer<%s>" (typeToDlangString typeMap elemType)
    | TupleType tupleTypes ->
        let joined =
            tupleTypes
            |> List.map (typeToDlangString typeMap)
            |> String.concat ", "
        sprintf "Tuple!(%s)" joined
    | TypedefType name -> name
    | MapType (keyType, valueType) ->
        let valueStr = typeToDlangString typeMap valueType
        let keyStr = typeToDlangString typeMap keyType
        sprintf "%s[%s]" valueStr keyStr
    | x ->
        sprintf "(typeToDlangString: not yet supported: %A)" x
        
let rec typeToName = function
    | UnitType -> failwith "typeToName: 'unit' type as name?"
    | Primitive prim ->
        match prim with
        | SizeT -> "SizeT"
        | Bool -> "Bool"
        | Int(signed, width) ->
            match signed, width with
            | true, Width8 -> "SByte"
            | false, Width8 -> "Byte"
            | true, Width16 -> "Short"
            | false, Width16 -> "UShort"
            | true, Width32 -> "Int"
            | false, Width32 -> "UInt"
            | true, Width64 -> "Long"
            | false, Width64 -> "ULong"
        | Float32 -> "Float"
        | Float64 -> "Double"
        | StringType -> "String"
    | StructType name -> upperFirst name
    | EnumType name -> upperFirst name
    | TypedefType name -> upperFirst name
    | TupleType tupleTypes ->
        let joined =
            tupleTypes
            |> List.map typeToName
            |> String.concat ""
        sprintf "%sTuple" joined
    | ArrayType niType ->
        sprintf "%sArray" (typeToName niType)
    | SetType niType ->
        sprintf "%sSet" (typeToName niType)
    | MapType (keyType, valueType) ->
        sprintf "%s%sMap" (typeToName keyType) (typeToName valueType)
    | BufferType elemType ->
        sprintf "Native%sBuffer" (typeToName elemType)
    | x -> sprintf "(typeToName: not yet supported: %A)" x
        
let argsJoined (typeMap: Map<string, UserOrGeneric>) (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToDlangString typeMap type_) name]
    |> String.concat ", "
    
let argNamesJoined (args: (string * NIType) list) =
    [for name, _ in args -> name]
    |> String.concat ", "
    
type PopValueContext =
    | NewVariable of name: string
    | ExistingVar of name: string
    | Return
    | SafeReturn
    
let popValue (writer: IFormatWriter) (level: int) (type_: NIType) (context: PopValueContext) =
    let write = writer.Write
    let contextPrefix =
        match context with
        | NewVariable name -> sprintf "auto %s =" name
        | ExistingVar name -> sprintf "%s =" name
        | Return -> "return"
        | SafeReturn -> "auto __ret ="
    match type_ with
    | UnitType ->
        match context with
        | Return -> () // nothing'
        | _ -> failwith "tried to pop a Unit value in a non-Return context, why?"
    | Primitive prim ->
        match prim with
        | SizeT -> write level "%s nativeimplclient.popSizeT();" contextPrefix
        | Bool -> write level "%s nativeimplclient.popBool();" contextPrefix
        | Int(signed, width) ->
            write level "%s nativeimplclient.pop%sInt%d();" contextPrefix (if signed then "" else "U") (widthToInt width)
        | Float32 -> write level "%s nativeimplclient.popFloat();" contextPrefix
        | Float64 -> write level "%s nativeimplclient.popDouble();" contextPrefix
        | StringType -> write level "%s nativeimplclient.popString();" contextPrefix
    | OpaqueHandleType name ->
        write level "%s %s.pop();" contextPrefix (upperFirst name)
    | InterfaceType ifaceName ->
        write level "%s %s__Pop();" contextPrefix (upperFirst ifaceName)
    | DelegateType name ->
        write level "%s %s__Pop();" contextPrefix (upperFirst name)
    | ArrayType niType ->
        match niType with
        | Primitive prim ->
            match prim with
            | Bool -> write level "%s nativeimplclient.popBoolArray();" contextPrefix
            | Int(signed, width) ->
                write level "%s nativeimplclient.pop%sInt%dArray();" contextPrefix (if signed then "" else "U") (widthToInt width)
            | Float32 -> write level "%s nativeimplclient.popFloatArray();" contextPrefix
            | Float64 -> write level "%s nativeimplclient.popDoubleArray();" contextPrefix
            | StringType -> write level "%s nativeimplclient.popStringArray();" contextPrefix
            | _ -> write level "// popValue not yet supported: array of prim %A" prim
        | EnumType name -> write level "%s __%sArray__Pop();" contextPrefix (upperFirst name)
        | StructType name -> write level "%s __%sArray__Pop();" contextPrefix (upperFirst name)
        | ArrayType subType -> write level "%s __%sArray__Pop();" contextPrefix (typeToName (ArrayType subType)) // sigh, normalize all these already - doing it so many different ways...
        | SetType subType -> write level "%s __%sArray__Pop();" contextPrefix (typeToName (ArrayType subType))
        | MapType _ -> write level "%s __%s__Pop();" contextPrefix (typeToName type_)
        | TupleType _ -> write level "%s __%s__Pop();" contextPrefix (typeToName type_)
        | _ -> write level "// popValue not yet supported: array of %A" niType
    | MapType _ ->
        write level "%s __%s__Pop();" contextPrefix (typeToName type_)
    | StructType name ->
        write level "%s %s__Pop();" contextPrefix (upperFirst name)
    | TupleType _ ->
        write level "%s __%s__Pop();" contextPrefix (typeToName type_)
    | SetType _ ->
        write level "%s __%s__Pop();" contextPrefix (typeToName type_)
    | BufferType _ ->
        write level "%s __%s__Pop();" contextPrefix (typeToName type_)
    | EnumType name ->
        write level "%s %s__Pop();" contextPrefix (upperFirst name)
    | SumType name ->
        write level "%s __%s__Pop();" contextPrefix (upperFirst name)
    | TypedefType name ->
        write level "%s %s__Pop();" contextPrefix (upperFirst name)
    | _ ->
        write level "// popValue %A not yet supported" type_
    match context with
    | SafeReturn ->
        write level "nativeimplclient.serverClearSafetyArea();"
        write level "return __ret;"
    | _ ->
        ()
        
type PushIsReturn =
    | Direct of value: bool
    | Indirect of name: string
    | NotApplicable

let pushValue (writer: IFormatWriter) (level: int) (type_: NIType) (content: string) (isReturn: PushIsReturn) =
    let write = writer.Write
    let isReturnString() =
        match isReturn with
        | Direct value -> if value then "true" else "false"
        | Indirect name -> name
        | NotApplicable -> failwith "pushValue: 'NotApplicable' isReturn was consumed"
    match type_ with
    | UnitType ->
        write level "%s;" content // content directly (method invocation etc)
    | Primitive prim ->
        match prim with
        | SizeT -> write level "nativeimplclient.pushSizeT(%s);" content
        | Bool -> write level "nativeimplclient.pushBool(%s);" content
        | Int(signed, width) ->
            write level "nativeimplclient.push%sInt%d(%s);" (if signed then "" else "U") (widthToInt width) content
        | Float32 -> write level "nativeimplclient.pushFloat(%s);" content
        | Float64 -> write level "nativeimplclient.pushDouble(%s);" content
        | StringType -> write level "nativeimplclient.pushString(%s);" content
    | OpaqueHandleType name ->
        write level "(%s).push();" content
    | InterfaceType name ->
        write level "%s__Push(%s, %s);" (upperFirst name) content (isReturnString())
    | DelegateType name ->
        write level "%s__Push(%s);" (upperFirst name) content
    | ArrayType niType ->
        match niType with
        | Primitive prim ->
            match prim with
            | Bool -> write level "nativeimplclient.pushBoolArray(%s);" content
            | Int(signed, width) ->
                write level "nativeimplclient.push%sInt%dArray(%s);" (if signed then "" else "U") (widthToInt width) content
            | Float32 -> write level "nativeimplclient.pushFloatArray(%s);" content
            | Float64 -> write level "nativeimplclient.pushDoubleArray(%s);" content
            | StringType -> write level "nativeimplclient.pushStringArray(%s);" content
            | _ -> write level "// pushValue not yet supported: Array of prim %A" prim
        | EnumType name -> write level "__%sArray__Push(%s);" (upperFirst name) content
        | StructType name -> write level "__%sArray__Push(%s, %s);" (upperFirst name) content (isReturnString())
        | TypedefType name -> write level "__%sArray__Push(%s, %s);" (upperFirst name) content (isReturnString())
        | ArrayType subType -> write level "__%sArray__Push(%s, %s);" (typeToName (ArrayType subType)) content (isReturnString())
        | SetType subType -> write level "__%sArray__Push(%s, %s);" (typeToName (ArrayType subType)) content (isReturnString())
        | MapType _ -> write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
        | TupleType _ -> write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
        | _ -> write level "// pushValue not yet supported: Array of %A" niType
    | EnumType name ->
        write level "%s__Push(%s);" name content
    | SumType name ->
        write level "__%s__Push(%s, %s);" name content (isReturnString())
    | StructType name ->
        write level "%s__Push(%s, %s);" name content (isReturnString())
    | TupleType _ ->
        write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
    | SetType _ ->
        write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
    | BufferType _ ->
        write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
    | TypedefType name ->
        write level "%s__Push(%s, %s);" name content (isReturnString())
    | MapType _ ->
        write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
    | _ ->
        write level "// pushValue %A not yet supported" type_
        
let renderLibraryUserTypes (writer: IFormatWriter) (containsRefs: NIType -> bool) (name: string) (content: UserOrGeneric) (typeMap: Map<string, UserOrGeneric>) =
    let write, blank = writer.Write, writer.Blank
    //
    blank()
    match content with
    | UserOrGeneric.FwdInterface ->
        () // Dlang doesn't require forward declarations
    | UserOrGeneric.User userType ->
        match userType with
        | OpaqueHandleDef ->
            write 0 "struct %s {" (upperFirst name)
            write 0 "private:"
            write 1 "void* _ptr;"
            write 1 "this(void* ptr) {"
            write 2 "_ptr = ptr;"
            write 1 "}"
            write 1 "void push() {"
            write 2 "nativeimplclient.pushPtr(_ptr);"
            write 1 "}"
            write 1 "static %s pop() {" (upperFirst name)
            write 2 "auto ptr = nativeimplclient.popPtr();"
            write 2 "return %s(ptr);" (upperFirst name)
            write 1 "}"
            write 0 "}"
        | SumTypeDef sumTypeValues ->
            // definition =========
            write 0 "struct %s {" (upperFirst name)
            // tag ----------------
            write 1 "private enum Tag {"
            for value, maybeComma in sumTypeValues |> withTrailingCommas do
                write 2 "%s%s" value.Name maybeComma
            write 1 "}"
            // value defs ---------
            for value in sumTypeValues do
                write 1 "struct %s {" (upperFirst value.Name)
                for argName, argType in value.Args do
                    write 2 "%s %s;" (typeToDlangString typeMap argType) argName
                // 'make' func ----
                write 2 "static %s make(%s) {" (upperFirst name) (argsJoined typeMap value.Args)
                write 3 "return %s(cast(%s.T) %s(%s));" (upperFirst name) (upperFirst name) value.Name (argNamesJoined value.Args)
                write 2 "}"
                write 1 "}"
            // sumtype proper -----
            let joinedValueNames =
                sumTypeValues
                |> List.map (fun v -> v.Name)
                |> String.concat ","
            write 1 "alias T = SumType!(%s);" joinedValueNames
            write 1 "T value;"
            write 1 "alias value this;"
            write 0 "}"
            // PUSH
            blank()
            write 0 "private void __%s__Push(%s value, bool isReturn) {" (upperFirst name) (upperFirst name)
            write 1 "value.match!("
            for (i, value), maybeComma in sumTypeValues |> List.indexed |> withTrailingCommas do
                let varName = sprintf "v%d" (i + 1)
                write 2 "(%s.%s %s) {" (upperFirst name) value.Name varName
                for argName, argType in value.Args |> List.rev do
                    pushValue writer 3 argType (sprintf "%s.%s" varName argName) (Indirect "isReturn")
                pushValue writer 3 (Primitive (Int (true, Width32))) (sprintf "%s.Tag.%s" (upperFirst name) value.Name) NotApplicable
                write 2 "}%s" maybeComma
            write 1 ");"
            write 0 "}"
            // POP
            blank()
            write 0 "private %s __%s__Pop() {" (upperFirst name) (upperFirst name)
            write 1 "auto tag = cast(%s.Tag) nativeimplclient.popInt32();" (upperFirst name)
            write 1 "switch (tag) {"
            for value in sumTypeValues do
                write 2 "case %s.Tag.%s: {" (upperFirst name) value.Name
                for argName, argType in value.Args do
                    popValue writer 3 argType (NewVariable argName)
                write 3 "return %s.%s.make(%s);" (upperFirst name) value.Name (argNamesJoined value.Args)
                write 2 "}"
            write 2 "default:"
            write 3 "assert(0);"
            write 1 "}"
            write 0 "}"
        | _ ->
            failwithf "renderLibraryUserTypes: usertype [%A] not yet supported" userType
    | _ ->
        failwithf "renderLibraryUserTypes: unsupported type [%A]" content

let writeLibrary (m: Module) (writer: IFormatWriter) =
    let write, blank = writer.Write, writer.Blank
    
    let moduleMethods =
        m.Defs
        |> List.choose (function
            | FunctionDef namedFunction -> Some namedFunction
            | _ -> None)
    let delegates =
        m.Defs
        |> List.choose (function
            | UserType(name, DelegateDef delegateInfo) -> Some (name, delegateInfo)
            | _ -> None)
    let interfaces =
        m.Defs
        |> List.choose (function
            | UserType(name, InterfaceDef namedFunctions) -> Some (name, namedFunctions)
            | _ -> None)
    let exceptions =
        m.Defs
        |> List.choose (function
            | UserType(name, ExceptionDef fields) -> Some (name, fields)
            | _ -> None)
    let globals =
        m.Defs
        |> List.choose (function
            | Global(name, niType) -> Some (name, niType)
            | _ -> None)

    write 0 "module %s;" (m.Name |> lower)
    blank()
    write 0 "import std.stdio;"
    write 0 "import std.conv: to;"
    write 0 "import std.sumtype;"
    write 0 "import nativeimplclient;"
    write 0 "import simpleset: Set;"
    blank()
    write 0 "private:"
    blank()
    write 0 "ModuleRef _module;"
    
    let typeMap =
        userTypesAndGenerics m
        
    // top level functions
    for namedFunc in moduleMethods do
        write 0 "ModuleMethodRef _%s;" namedFunc.Name
        
    // interfaces
    for name, methods in interfaces do
        blank()
        write 0 "InterfaceRef _%s;" (lowerFirst name)
        for method in methods do
            write 0 "InterfaceMethodRef _%s_%s;" (lowerFirst name) method.Name
            
    // exceptions
    if not exceptions.IsEmpty then
        blank()
        for name, _ in exceptions do
            write 0 "ExceptionRef _%s;" (lowerFirst name)
        
    // globals declaration (private storage)
    if not globals.IsEmpty then
        blank()
        for name, type_ in globals do
            write 0 "%s _%s;" (typeToDlangString typeMap type_) (lowerFirst name)
           
    // PUBLIC STUFF ================================================================================================================= 
    blank()
    write 0 "public:"
    
    // user types ====================
    let containsRefsMap =
        createContainsRefsMap typeMap
    let containsRefs (type_: NIType) =
        containsRefsMap.TryFind(typeToInternalKey type_)
        |> Option.defaultValue false
    let renderFunc =
        renderLibraryUserTypes writer containsRefs
    renderUserTypes typeMap renderFunc
    
    // public globals accessors
    if not globals.IsEmpty then
        blank()
        for name, type_ in globals do
            write 0 "%s %s() { return _%s; }" (typeToDlangString typeMap type_) (lowerFirst name) (lowerFirst name)
            
    // top-level functions ===========
    for namedFunc in moduleMethods do
        blank()
        write 0 "%s %s(%s) {" (typeToDlangString typeMap namedFunc.Returns) (lowerFirst namedFunc.Name) (argsJoined typeMap namedFunc.Args)
        // push args in reverse
        for argName, argType in namedFunc.Args |> List.rev do
            pushValue writer 1 argType argName (Direct false)
        // invoke remote
        if namedFunc.Throws.IsEmpty then
            write 1 "nativeimplclient.invokeModuleMethod(_%s);" (lowerFirst namedFunc.Name)
        else
            write 1 "nativeimplclient.invokeModuleMethodWithExceptions(_%s);" (lowerFirst namedFunc.Name)
        // return pop
        popValue writer 1 namedFunc.Returns (if containsRefs namedFunc.Returns then SafeReturn else Return)
        write 0 "}"
            
    // static init + shutdown ====================================================
    let maybeModuleInit =
        moduleMethods
        |> List.tryFind (fun nf -> nf.Name = "moduleInit")
    let maybeModuleShutdown =
        moduleMethods
        |> List.tryFind (fun nf -> nf.Name = "moduleShutdown")
    blank()
    
    match maybeModuleInit with
    | Some initFunc ->
        // if user-provided init, then we use (and convey) its custom return value
        write 0 "%s init(%s) {" (typeToDlangString typeMap initFunc.Returns) (argsJoined typeMap initFunc.Args)
    | None ->
        write 0 "void init() {"
        
    write 1 "assert(nativeimplclient.init() == 0, \"nativeimplclient.init() failed\");"
    write 1 "_module = getModule(\"%s\");" m.Name
    
    // ========= module init guts ================
    
    // fetch globals
    if not globals.IsEmpty then
        blank()
        write 1 "nativeimplclient.pushModuleConstants(_module);"
        for name, type_ in globals do
            popValue writer 1 type_ (sprintf "_%s" name |> lowerFirst |> ExistingVar)
            
    // module method handles
    if not moduleMethods.IsEmpty then
        blank()
        for namedFunc in moduleMethods do
            write 1 "_%s = nativeimplclient.getModuleMethod(_module, \"%s\");" (lowerFirst namedFunc.Name) (lowerFirst namedFunc.Name)
            
    // exceptions
    if not exceptions.IsEmpty then
        for name, _ in exceptions do
            blank()
            write 1 "_%s = nativeimplclient.getException(_module, \"%s\");" (lowerFirst name) name
            write 1 "nativeimplclient.setExceptionBuilder(_%s, () => %s.pop());" (lowerFirst name) name
            
    // interface method handles, wrappers
    if not interfaces.IsEmpty then
        for ifaceName, methods in interfaces do
            blank()
            write 1 "_%s = nativeimplclient.getInterface(_module, \"%s\");" (lowerFirst ifaceName) ifaceName
            // methods
            if not methods.IsEmpty then
                for method in methods do
                    write 1 "_%s_%s = nativeimplclient.getInterfaceMethod(_%s, \"%s\");" (lowerFirst ifaceName) (lowerFirst method.Name) (lowerFirst ifaceName) method.Name
            // method wrappers
            for method in methods do
                blank()
                write 1 "nativeimplclient.setClientMethodWrapper(_%s_%s, (ClientObject obj) {" (lowerFirst ifaceName) method.Name
                write 2 "auto inst = cast(Client%s) obj;" ifaceName
                // pop args
                for argName, argType in method.Args do
                    popValue writer 2 argType (NewVariable argName)
                // exec + return
                let invocation =
                    sprintf "inst.%s(%s)" (lowerFirst method.Name) (argNamesJoined method.Args)
                if method.Throws.IsEmpty then
                    pushValue writer 2 method.Returns invocation (Direct true)
                else
                    write 2 "try {"
                    pushValue writer 3 method.Returns invocation (Direct true)
                    for exc in method.Throws do
                        match exc with
                        | ExceptionType _ ->
                            write 2 "} catch (%s e) {" (typeToDlangString typeMap exc)
                            write 3 "e.setAndPush();"
                            write 2 "}"
                        | _ ->
                            failwith "Dlang interface wrappers: 'throws' contained non-exception type"
                write 1 "});"
    
    // ========= end module init guts ============
    
    // invoke user's ModuleInit, if it exists
    match maybeModuleInit with
    | Some initFunc ->
        blank()
        write 1 "return moduleInit(%s);" (argNamesJoined initFunc.Args)
    | None ->
        ()
    write 0 "}" // end module init
    
    // shutdown
    blank()
    write 0 "void shutdown() {"
    match maybeModuleShutdown with
    | Some _ ->
        write 1 "moduleShutdown();"
    | None ->
        ()
    write 1 "nativeimplclient.shutdown();"
    write 0 "}"
    
    // dumptables
    blank()
    write 0 "void dumpTables() {"
    write 1 "nativeimplclient.dumpTables();"
    write 0 "}"

let generate (m: Module) =
    printfn "Dlang writing testlibrary.d"
    use output =
        let writer = new StreamWriter(@"F:\Workbench\nativeimpl\client\dlang\testlibrary\source\testlibrary.d")
        new SimpleFormatWriter(writer)
    writeLibrary m output
