module Generators.Frontend.DlangGenerator

open System.IO
open AppCommon
open Generators.Common
open Parser
open Util

type DlangRenderContext = {
    BaseNamespace: string list
    RenderContext: RenderContext
    OpaqueMethodContext: OpaqueInfo option         // rendering an opaque method (pop/invocation/push) at the current group/path, prone to extra collisions on the push/pop statements
} with
    member this.BaseNamespaceJoined =
        joinNameParts "." this.BaseNamespace
    member this.ModuleContext =
        this.RenderContext.ModuleContext
    member this.GroupContext =
        this.RenderContext.GroupContext
    member this.NavigateToSub (name: string) =
        let nextRenderContext =
            this.RenderContext.NavigateToSub name
        { this with RenderContext = nextRenderContext }

let resolvedNameToString (context: DlangRenderContext) (resolvedName: ResolvedName) (mode: ResolveNameStringMode) =
    let qualification =
        let prefix =
            if mode = GenericIdentifierMode then
                // colliding with local symbols not a concern in this context, so we can use any (or probably even no) prefix
                ["G"]
            else
                context.BaseNamespace
        Forced prefix
    resolvedNameToStringAbstract "." (fun name _ -> name) None qualification context.RenderContext resolvedName mode

let constExprToString (context: DlangRenderContext) =
    let config =
        { NullStr = "null"
          ScopeResStr = "." }
    let strFunc resolvedName =
        resolvedNameToString context resolvedName TypeMode
    typedConstExprString config strFunc

let typeToDlangString (context: DlangRenderContext) (outerType: NIType) =
    let rec recInner = function
        | UnitType -> "void"
        | Primitive prim ->
            match prim with
            | SizeT -> "size_t"
            | Bool -> "bool"
            | Int(signed, intWidth) ->
                match signed, intWidth with
                | true, Width8 -> "sbyte"
                | false, Width8 -> "byte"
                | true, Width16 -> "short"
                | false, Width16 -> "ushort"
                | true, Width32 -> "int"
                | false, Width32 -> "uint"
                | true, Width64 -> "long"
                | false, Width64 -> "ulong"
            | Float32 -> "float"
            | Float64 -> "double"
            | StringType -> "string"
        | Resolved (resolvedName, resolvedType) ->
            match resolvedType with
            | FwdOpaqueType ->
                match requiredSymbolLookup context.ModuleContext resolvedName with
                | FwdOpaqueDef parts ->
                    sprintf "%s" (joinNameParts "." (context.BaseNamespace @ parts))
                | _ ->
                    failwith "typeToDlangString: failed to find fwdopaque definition"
            | TypedefType ->
                // no C# typedefs so we just render the type directly
                match requiredSymbolLookup context.ModuleContext resolvedName with
                | TypeDef actual ->
                    recInner actual
                | _ ->
                    failwithf "typeToDlangString: failed to find typedef definition"
            | _ ->
                resolvedNameToString context resolvedName TypeMode
        | ArrayType elemType -> sprintf "%s[]" (recInner elemType)
        // | SetType elemType -> sprintf "HashSet<%s>" (recInner elemType)
        // | BufferType elemType -> sprintf "INativeBuffer<%s>" (recInner elemType)
        | TupleType tupleTypes ->
            let joined =
                tupleTypes
                |> List.map recInner
                |> String.concat ", "
            sprintf "Tuple!(%s)" joined
        | MapType (keyType, valueType) ->
            sprintf "%s[%s]" (recInner valueType) (recInner keyType)
        // | OptionType elemType ->
        //     sprintf "Maybe<%s>" (recInner elemType)
        // | ResultType (succType, errType) ->
        //     sprintf "Result<%s,%s>" (recInner succType) (recInner errType)
        | x ->
            sprintf "(typeToCsString: not yet supported: %A)" x
    // go
    recInner outerType

let rec newDlangArray (context: DlangRenderContext) (ofType: NIType) (countVar: string) =
    match ofType with
    | ArrayType niType ->
        sprintf "%s[]" (newDlangArray context niType countVar)
    | _ ->
        sprintf "new %s[%s]" (typeToDlangString context ofType) countVar

let typeToName (context: DlangRenderContext) (outerType: NIType) =
    let rec recInner (inGeneric: bool) = function
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
        | Resolved (resolvedName, resolvedType) ->
            resolvedNameToString context resolvedName (if inGeneric then GenericIdentifierMode else IdentifierMode)
        | TupleType tupleTypes ->
            let joined =
                tupleTypes
                |> List.map (recInner true)
                |> String.concat ""
            sprintf "%s_Tuple" joined
        | ArrayType niType ->
            sprintf "%s_Array" (recInner true niType)
        | SetType niType ->
            sprintf "%s_Set" (recInner true niType)
        | MapType (keyType, valueType) ->
            sprintf "%s_%s_Map" (recInner true keyType) (recInner true valueType)
        | OptionType elemType ->
            sprintf "%s_Option" (recInner true elemType)
        | ResultType (succType, errType) ->
            sprintf "%s_%s_Result" (recInner true succType) (recInner true errType)
        | BufferType elemType ->
            sprintf "Native_%s_Buffer" (recInner true elemType)
        | x -> sprintf "(typeToName: not yet supported: %A)" x
    recInner false outerType

let typeWithPrefix (context: DlangRenderContext) (outerType: NIType) (prefix: string) =
    // function for appropriately-qualified path to parent interface, but with <prefix> inserted just before the final part (eg org.whatever.<prefix>Thing)
    // (note, from C# generator, not yet tested :)
    let x = typeToDlangString context outerType
    let parts = x.Split(".")
    let lastIndex = parts.Length - 1
    parts[lastIndex] <- prefix + parts[lastIndex]
    String.concat "." parts

let joinedArgs (context: DlangRenderContext) (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToDlangString context type_) name]
    |> String.concat ", "

let joinedNameFuncArgs (context: DlangRenderContext) (args: NamedFunctionArg list) =
    [for arg in args ->
        match arg.DefaultValue with
        | Some value ->
            sprintf "%s %s = %s" (typeToDlangString context arg.Type) arg.Name (constExprToString context value)
        | None ->
            sprintf "%s %s" (typeToDlangString context arg.Type) arg.Name ]
    |> String.concat ", "

let joinedArgNames (args: (string * NIType) list) =
    [for name, _ in args -> name]
    |> String.concat ", "

let joinedNamedFuncArgNames (args: NamedFunctionArg list) =
    [for arg in args -> arg.Name]
    |> String.concat ", "

let fieldsJoined (context: DlangRenderContext) (fields: NamedField list) =
    [for f in fields -> sprintf "%s %s" (typeToDlangString context f.Type_) f.Name]
    |> String.concat ", "

let fieldNamesJoined (fields: NamedField list) =
    [for f in fields -> f.Name]
    |> String.concat ", "

type PopValueContext =
    | NewVariable of name: string
    | ExistingVar of name: string
    | Return
    | SafeReturn

let popValue (context: DlangRenderContext) (writer: IFormatWriter) (level: int) (type_: NIType) (popContext: PopValueContext) =
    let write = writer.Write
    let contextPrefix =
        match popContext with
        | NewVariable name -> sprintf "auto %s =" name
        | ExistingVar name -> sprintf "%s =" name
        | Return -> "return"
        | SafeReturn -> "auto __ret ="
    match type_ with
    | UnitType ->
        match popContext with
        | Return -> () // nothing'
        | _ -> failwith "tried to pop a Unit value in a non-Return context, why?"
    | Primitive prim ->
        match prim with
        | SizeT -> write level "%s ni.popSizeT();" contextPrefix
        | Bool -> write level "%s ni.popBool();" contextPrefix
        | Int(signed, width) ->
            write level "%s ni.pop%sInt%d();" contextPrefix (if signed then "" else "U") (widthToInt width)
        | Float32 -> write level "%s ni.popFloat();" contextPrefix
        | Float64 -> write level "%s ni.popDouble();" contextPrefix
        | StringType -> write level "%s ni.popString();" contextPrefix
    | Resolved (resolvedName, resolvedType) ->
        let name =
            match resolvedType with
            | FwdOpaqueType ->
                // special treatment for these:
                match requiredSymbolLookup context.ModuleContext resolvedName with
                | FwdOpaqueDef parts ->
                    sprintf "%s" (joinNameParts "." (context.BaseNamespace @ parts))
                | _ ->
                    failwithf "Dlang popValue: FwdOpaque trouble, should never happen"
            | _ ->
                // all other types:
                resolvedNameToString context resolvedName TypeMode
        write level "%s %s__pop();" contextPrefix name
    | ArrayType niType ->
        match niType with
        | Primitive prim ->
            match prim with
            | Bool -> write level "%s ni.popBoolArray();" contextPrefix
            | Int(signed, width) ->
                write level "%s ni.pop%sInt%dArray();" contextPrefix (if signed then "" else "U") (widthToInt width)
            | Float32 -> write level "%s ni.popFloatArray();" contextPrefix
            | Float64 -> write level "%s ni.popDoubleArray();" contextPrefix
            | StringType -> write level "%s ni.popStringArray();" contextPrefix
            | _ -> write level "// popValue not yet supported: array of prim %A" prim
        | Resolved _ ->
            write level "%s __%s__pop();" contextPrefix (typeToName context (ArrayType niType))
        | ArrayType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_) // sigh, normalize all these already - doing it so many different ways...
        | SetType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_)
        | MapType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_)
        | TupleType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_)
        | OptionType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_)
        | ResultType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_)
        | _ -> write level "// popValue not yet supported: array of %A" niType
    | MapType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | TupleType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | SetType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | OptionType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | ResultType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | BufferType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | _ ->
        write level "// popValue %A not yet supported" type_
    match popContext with
    | SafeReturn ->
        write level "ni.serverClearSafetyArea();"
        write level "return __ret;"
    | _ ->
        ()

type PushIsReturn =
    | Direct of value: bool
    | Indirect of name: string
    | NotApplicable

let pushValue (context: DlangRenderContext) (writer: IFormatWriter) (level: int) (type_: NIType) (content: string) (isReturn: PushIsReturn) =
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
        | SizeT -> write level "ni.pushSizeT(%s);" content
        | Bool -> write level "ni.pushBool(%s);" content
        | Int(signed, width) ->
            write level "ni.push%sInt%d(%s);" (if signed then "" else "U") (widthToInt width) content
        | Float32 -> write level "ni.pushFloat(%s);" content
        | Float64 -> write level "ni.pushDouble(%s);" content
        | StringType -> write level "ni.pushString(%s);" content
    | Resolved(resolvedName, resolvedType) ->
        let name =
            match resolvedType with
            | FwdOpaqueType ->
                // special treatment for fwd opaques:
                match requiredSymbolLookup context.ModuleContext resolvedName with
                | FwdOpaqueDef parts ->
                    sprintf "%s" (joinNameParts "." (context.BaseNamespace @ parts))
                | _ ->
                    failwithf "Dlang pushValue: FwdOpaqueType trouble, shouldn't happen"
            | _ ->
                // all other types:
                resolvedNameToString context resolvedName TypeMode
        match resolvedType with
        | StructType | InterfaceType | TypedefType | SumType | OptionsType ->
            // types that potentially contain refs, need to honor isReturn
            write level "%s__push(%s, %s);" name content (isReturnString())
        | _ ->
            write level "%s__push(%s);" name content
    | ArrayType niType ->
        match niType with
        | Primitive prim ->
            match prim with
            | Bool -> write level "ni.pushBoolArray(%s);" content
            | Int(signed, width) ->
                write level "ni.push%sInt%dArray(%s);" (if signed then "" else "U") (widthToInt width) content
            | Float32 -> write level "ni.pushFloatArray(%s);" content
            | Float64 -> write level "ni.pushDoubleArray(%s);" content
            | StringType -> write level "ni.pushStringArray(%s);" content
            | _ -> write level "// pushValue not yet supported: Array of prim %A" prim
        | Resolved (_, resolvedType) ->
            match resolvedType with
            | StructType | InterfaceType | TypedefType | SumType | OptionsType ->
                write level "__%s__push(%s, %s);" (typeToName context (ArrayType niType)) content (isReturnString())
            | _ ->
                write level "__%s__push(%s);" (typeToName context (ArrayType niType)) content
        | ArrayType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | SetType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | MapType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | TupleType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | OptionType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | ResultType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | _ -> write level "// pushValue not yet supported: Array of %A" niType
    | TupleType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | SetType niType ->
        match niType with
        | Primitive _ ->
            write level "__%s__push(%s);" (typeToName context type_) content
        | Resolved(_, resolvedType) ->
            match resolvedType with
            | StructType | InterfaceType | TypedefType | SumType | OptionsType ->
                write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
            | _ ->
                write level "__%s__push(%s);" (typeToName context type_) content
        | _ ->
            write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | BufferType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | MapType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | OptionType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | ResultType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | _ ->
        write level "// pushValue %A not yet supported" type_

// TODO: ArrayOrSetRenderer etc
// TODO: renderGeneric

let joinedTypes (context: DlangRenderContext) (typesList: NIType list) =
    typesList
    |> List.map (typeToDlangString context)
    |> String.concat ", "

let rec renderUserType (context: DlangRenderContext) (writer: IFormatWriter) (name: string) (userType: UserType) =
    let write, blank = writer.Write, writer.Blank

    match userType with
    | DelegateDef info ->
        blank()
        write 0 "// %s type + push/pop ======" name
        let throwsList =
            match info.Throws with
            | [] -> ""
            | _ -> " // throws " + joinedTypes context info.Throws
        write 0 "public alias %s = %s delegate(%s);%s" name (typeToDlangString context info.Returns) (joinedArgs context info.Args) throwsList
        // push
        blank()
        write 0 "package void %s__push(%s func) {" (lowerFirst name) name
        write 1 "void wrapper() {"
        for argName, argType in info.Args do
            popValue context writer 2 argType (NewVariable argName)
        let invocation =
            sprintf "func(%s)" (joinedArgNames info.Args)
        if info.Throws.IsEmpty then
            pushValue context writer 2 info.Returns invocation (Direct true)
        else
            write 2 "try {"
            pushValue context writer 3 info.Returns invocation (Direct true)
            for exc in info.Throws do
                write 2 "} catch (%s e) {" (typeToDlangString context exc)
                write 3 "e.PushAndSet();"
            write 2 "}"
        write 1 "}" // end wrapper
        write 1 "ni.pushClientFuncVal(&wrapper, func.ptr, func.funcptr);"
        write 0 "}"
        // pop
        blank()
        write 0 "package %s %s__pop() {" name (lowerFirst name)
        write 1 "auto f = ni.popServerFuncVal();"
        write 1 "%s wrapper(%s) {" (typeToDlangString context info.Returns) (joinedArgs context info.Args)
        for argName, argType in info.Args |> List.rev do
            pushValue context writer 2 argType argName (Direct false)
        if info.Throws.IsEmpty then
            write 2 "f.exec();"
        else
            write 2 "f.execWithExceptions();"
        popValue context writer 2 info.Returns Return
        write 1 "}"
        write 1 "return &wrapper;"
        write 0 "}"
        write 0 "// ====== end %s delegate" name
    | _ ->
        write 0 "// unhandled user type %s" name

and renderGroupCommon (context: DlangRenderContext) (writer: IFormatWriter) =
    let write, blank = writer.Write, writer.Blank

    let groupContext =
        context.GroupContext

    // blank()
    // write 0 "private:"
    // blank()

    // declare top level function handles
    for namedFunc in groupContext.ModuleMethods do
        write 0 "ni.ModuleMethodHandle _%s;" namedFunc.WrapperName

    for name, info in groupContext.Opaques do
        // instance methods
        for method in info.Methods do
            write 0 "ni.ModuleMethodHandle _%s_%s;" (lowerFirst name) method.WrapperName
        // synthesized dispose method
        if info.NoDispose then
            ()
        else
            write 0 "ni.ModuleMethodHandle _%s_dispose;" (lowerFirst name)

    // interfaces
    for name, info in groupContext.Interfaces do
        write 0 "ni.InterfaceHandle _%s;" (lowerFirst name)
        for method in info.Methods do
            write 0 "ni.InterfaceMethodHandle _%s_%s;" (lowerFirst name) method.WrapperName

    // exceptions
    for name, _ in groupContext.Exceptions do
        write 0 "ni.ExceptionHandle _%s;" (lowerFirst name)

    // globals declaration
    for name, type_ in groupContext.Globals do
        write 0 "%s %s;" (typeToDlangString context type_) (lowerFirst name)

    // blank()
    // write 0 "public:"
    // blank()

    // top-level functions ===========
    for namedFunc in groupContext.ModuleMethods do
        blank()
        write 0 "public %s %s(%s)" (typeToDlangString context namedFunc.Returns) (lowerFirst namedFunc.Name) (joinedNameFuncArgs context namedFunc.Args)
        write 0 "{"
        // push args in reverse
        for arg in namedFunc.Args |> List.rev do
            pushValue context writer 1 arg.Type arg.Name (Direct false)
        // invoke remote
        if namedFunc.Throws.IsEmpty then
            write 1 "ni.invokeModuleMethod(_%s);" namedFunc.WrapperName
        else
            write 1 "ni.nvokeModuleMethodWithExceptions(_%s);" namedFunc.WrapperName
        // return pop
        popValue context writer 1 namedFunc.Returns (if containsRefs context.ModuleContext namedFunc.Returns then SafeReturn else Return)
        write 0 "}"

    // render user types + nested groups in order of declaration
    for def in groupContext.Defs do
        match def with
        | UserType(name, userType) ->
            renderUserType context writer name userType
        | Group(name, _) ->
            let group =
                groupContext.SubGroups[name]
            let nextContext =
                let nextSubRenderContext =
                    { context.RenderContext with GroupContext = group }
                { context with RenderContext = nextSubRenderContext } // why doesn't nested update work for us??
            write 0 "public static class %s" group.Name
            write 0 "{"
            renderGroupCommon nextContext (writer.WithExtraIndent 1)
            write 0 "}"
            // renderNestedGroup nextContext writer
        | _ ->
            ()

let writeModule (moduleContext: ModuleContext) (writer: IFormatWriter) (baseNamespace: string) (libraryName: string) (maybeModulePrefix: string option) (simpleName: string) =
    let write, blank = writer.Write, writer.Blank
    let m =
        moduleContext.Module
    let renderContext =
        { BaseNamespace = (baseNamespace.Split "." |> Array.toList)
          RenderContext = createRenderContext moduleContext
          OpaqueMethodContext = None }
    let root =
        renderContext.GroupContext

    let fullyQualifiedModulename =
        let prefix =
            maybeModulePrefix
            |> Option.map (fun prefix -> libraryName + "." + prefix)
            |> Option.defaultValue libraryName
        prefix + "." + simpleName
        |> _.ToLower()
    write 0 "module %s;" fullyQualifiedModulename
    blank()
    //
    // TODO: other module imports!
    //
    write 0 "import std.typecons: Tuple, tuple;"
    write 0 "import ni = %s.support.nativeimplclient;" (lower libraryName)
    blank()
    write 0 "private:"
    blank()
    write 0 "ni.ModuleHandle _module;"
    //
    // TODO: render generics just once, at root level
    //
    // now with the common group stuff:
    renderGroupCommon renderContext writer

    // TODO: init/shutdown methods


let projectDir (outParams: OutParams) =
    sprintf "%s/client/dlang/%s" outParams.Path (lower outParams.LibraryName)

let generateModule (context: ModuleContext) (outParams: OutParams) =
    let baseNamespace, maybeModulePrefix, simpleName =
        namespaceParts outParams context.Module.Name
    let moduleDir =
        let subPath =
            maybeModulePrefix
            |> Option.map (fun mp -> "/" + mp.Replace(".", "/"))
            |> Option.defaultValue ""
        sprintf "%s/source/%s%s" (projectDir outParams) (lower outParams.LibraryName) subPath
    Directory.CreateDirectory(moduleDir) |> ignore
    let filename =
        sprintf "%s/%s.d" moduleDir (simpleName.ToLower())
    let modulePrefixString =
        maybeModulePrefix
        |> Option.map (fun mp -> $"{mp}.")
        |> Option.defaultValue ""
    printfn "Dlang writing (%s.%s%s) [%s]" baseNamespace modulePrefixString simpleName filename
    use output =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer, 0)
    writeModule context output baseNamespace outParams.LibraryName maybeModulePrefix simpleName
