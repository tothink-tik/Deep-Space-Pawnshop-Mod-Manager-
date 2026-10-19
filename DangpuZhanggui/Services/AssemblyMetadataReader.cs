using System.Collections.Immutable;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Dangpu.Services;

public sealed class ModAssemblyInfo
{
    public string AssemblyName { get; set; } = "";
    public string AssemblyVersion { get; set; } = "";
    public string? MelonName { get; set; }
    public string? MelonVersion { get; set; }
    public string? Author { get; set; }
    public string? DownloadLink { get; set; }
    public string? GameName { get; set; }
    public string? GameDeveloper { get; set; }
    public bool HasMelonInfo { get; set; }
    public bool HasMelonGame { get; set; }
    public List<string> References { get; } = new();
    public List<string> Dependencies { get; } = new();
}

/// <summary>
/// 只读 PE 元数据，不加载程序集、不执行任何代码。
/// 用来读出 MelonInfo / MelonGame / 依赖声明，判断这个 dll 会不会被加载器认。
/// </summary>
public static class AssemblyMetadataReader
{
    public static ModAssemblyInfo? Read(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var pe = new PEReader(fs);
            if (!pe.HasMetadata) return null;
            var md = pe.GetMetadataReader();
            if (!md.IsAssembly) return null;

            var def = md.GetAssemblyDefinition();
            var info = new ModAssemblyInfo
            {
                AssemblyName = md.GetString(def.Name),
                AssemblyVersion = def.Version.ToString()
            };

            foreach (var handle in def.GetCustomAttributes())
            {
                try
                {
                    var ca = md.GetCustomAttribute(handle);
                    var typeName = AttributeTypeName(md, ca);
                    var value = ca.DecodeValue(new TypeNameProvider());

                    switch (typeName)
                    {
                        case "MelonLoader.MelonInfoAttribute":
                        {
                            info.HasMelonInfo = true;
                            var args = value.FixedArguments;
                            // 签名：(Type, name, version, author, downloadLink)，第一个参数是 typeof(...)
                            int i = 0;
                            if (args.Length > 0 && args[0].Type == "System.Type") i = 1;
                            if (args.Length > i) info.MelonName = args[i].Value as string;
                            if (args.Length > i + 1) info.MelonVersion = args[i + 1].Value as string;
                            if (args.Length > i + 2) info.Author = args[i + 2].Value as string;
                            if (args.Length > i + 3) info.DownloadLink = args[i + 3].Value as string;
                            break;
                        }
                        case "MelonLoader.MelonGameAttribute":
                        {
                            info.HasMelonGame = true;
                            var args = value.FixedArguments;
                            if (args.Length >= 2)
                            {
                                info.GameDeveloper = args[0].Value as string;
                                info.GameName = args[1].Value as string;
                            }
                            else if (args.Length == 1)
                            {
                                info.GameName = args[0].Value as string;
                            }
                            break;
                        }
                        case "MelonLoader.MelonAdditionalDependenciesAttribute":
                        case "MelonLoader.MelonOptionalDependenciesAttribute":
                        {
                            foreach (var a in value.FixedArguments)
                                foreach (var s in ReadStringArray(a))
                                    if (!string.IsNullOrWhiteSpace(s)) info.Dependencies.Add(s);
                            break;
                        }
                    }
                }
                catch
                {
                    // 单个特性解析失败不影响其它信息
                }
            }

            foreach (var rh in md.AssemblyReferences)
            {
                try
                {
                    var r = md.GetAssemblyReference(rh);
                    info.References.Add(md.GetString(r.Name));
                }
                catch { }
            }

            return info;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> ReadStringArray(CustomAttributeTypedArgument<string> arg)
    {
        switch (arg.Value)
        {
            case string s:
                yield return s;
                break;
            case ImmutableArray<CustomAttributeTypedArgument<string>> arr:
                foreach (var item in arr)
                    if (item.Value is string v) yield return v;
                break;
        }
    }

    private static string AttributeTypeName(MetadataReader md, CustomAttribute ca)
    {
        switch (ca.Constructor.Kind)
        {
            case HandleKind.MemberReference:
            {
                var mr = md.GetMemberReference((MemberReferenceHandle)ca.Constructor);
                return mr.Parent.Kind switch
                {
                    HandleKind.TypeReference => TypeRefName(md, (TypeReferenceHandle)mr.Parent),
                    HandleKind.TypeDefinition => TypeDefName(md, (TypeDefinitionHandle)mr.Parent),
                    _ => ""
                };
            }
            case HandleKind.MethodDefinition:
            {
                var m = md.GetMethodDefinition((MethodDefinitionHandle)ca.Constructor);
                return TypeDefName(md, m.GetDeclaringType());
            }
            default:
                return "";
        }
    }

    private static string TypeDefName(MetadataReader md, TypeDefinitionHandle handle)
    {
        var td = md.GetTypeDefinition(handle);
        var name = md.GetString(td.Name);
        var declaring = td.GetDeclaringType();
        if (!declaring.IsNil) return TypeDefName(md, declaring) + "+" + name;
        var ns = md.GetString(td.Namespace);
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    private static string TypeRefName(MetadataReader md, TypeReferenceHandle handle)
    {
        var tr = md.GetTypeReference(handle);
        var name = md.GetString(tr.Name);
        if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            return TypeRefName(md, (TypeReferenceHandle)tr.ResolutionScope) + "+" + name;
        var ns = md.GetString(tr.Namespace);
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    private sealed class TypeNameProvider : ICustomAttributeTypeProvider<string>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
        public string GetSystemType() => "System.Type";
        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => TypeDefName(reader, handle);
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => TypeRefName(reader, handle);
        public string GetTypeFromSerializedName(string name) => name;
        public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;
        public bool IsSystemType(string type) => type is "System.Type" or "Type";
    }
}
