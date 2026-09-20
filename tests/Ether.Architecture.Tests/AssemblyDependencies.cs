using System.Reflection;

using Mono.Cecil;

namespace Ether.Architecture.Tests;

/// <summary>
/// IL-level dependency scanner used by the architecture tests.
/// Reads type references (base types, interfaces, fields, properties, method
/// signatures, generic arguments and instructions) so layer violations are
/// detected even when they only appear through interfaces or generic arguments.
/// </summary>
internal static class AssemblyDependencies
{
    public static IReadOnlySet<string> CollectNamespaces(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        using var module = ModuleDefinition.ReadModule(assembly.Location);

        foreach (var type in module.GetTypes())
        {
            AddType(namespaces, type.BaseType);

            foreach (var implementation in type.Interfaces)
            {
                AddType(namespaces, implementation.InterfaceType);
            }

            foreach (var field in type.Fields)
            {
                AddType(namespaces, field.FieldType);
            }

            foreach (var property in type.Properties)
            {
                AddType(namespaces, property.PropertyType);
            }

            foreach (var method in type.Methods)
            {
                AddType(namespaces, method.ReturnType);

                foreach (var parameter in method.Parameters)
                {
                    AddType(namespaces, parameter.ParameterType);
                }

                if (!method.HasBody)
                {
                    continue;
                }

                foreach (var variable in method.Body.Variables)
                {
                    AddType(namespaces, variable.VariableType);
                }

                foreach (var instruction in method.Body.Instructions)
                {
                    switch (instruction.Operand)
                    {
                        case TypeReference typeReference:
                            AddType(namespaces, typeReference);
                            break;
                        case MethodReference methodReference:
                            AddType(namespaces, methodReference.DeclaringType);
                            AddGenericArguments(namespaces, methodReference);
                            break;
                        case FieldReference fieldReference:
                            AddType(namespaces, fieldReference.DeclaringType);
                            break;
                    }
                }
            }
        }

        return namespaces;
    }

    public static IReadOnlySet<string> CollectReferencedAssemblies(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        using var module = ModuleDefinition.ReadModule(assembly.Location);
        return module.AssemblyReferences
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static bool DependsOnNamespace(Assembly assembly, string namespaceRoot)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceRoot);

        return CollectNamespaces(assembly).Any(ns => Matches(ns, namespaceRoot));
    }

    public static bool Matches(string candidate, string namespaceRoot) =>
        candidate.Equals(namespaceRoot, StringComparison.Ordinal) ||
        candidate.StartsWith(namespaceRoot + ".", StringComparison.Ordinal);

    private static void AddType(ISet<string> namespaces, TypeReference? typeReference)
    {
        if (typeReference is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(typeReference.Namespace))
        {
            namespaces.Add(typeReference.Namespace);
        }

        switch (typeReference)
        {
            case GenericInstanceType generic:
                AddType(namespaces, generic.ElementType);
                foreach (var argument in generic.GenericArguments)
                {
                    AddType(namespaces, argument);
                }

                break;
            case ArrayType array:
                AddType(namespaces, array.ElementType);
                break;
            case ByReferenceType byReference:
                AddType(namespaces, byReference.ElementType);
                break;
            case PointerType pointer:
                AddType(namespaces, pointer.ElementType);
                break;
            case TypeSpecification specification:
                AddType(namespaces, specification.ElementType);
                break;
        }
    }

    private static void AddGenericArguments(ISet<string> namespaces, MethodReference methodReference)
    {
        if (methodReference is GenericInstanceMethod generic)
        {
            foreach (var argument in generic.GenericArguments)
            {
                AddType(namespaces, argument);
            }
        }
    }
}
