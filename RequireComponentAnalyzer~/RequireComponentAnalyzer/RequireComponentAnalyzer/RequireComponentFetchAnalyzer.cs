using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RequireComponentAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RequireComponentFetchAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "URC001";

        private const string Category = "Usage";

        // URC001: a public or [SerializeField]-private field whose type matches one of the class's
        // [RequireComponent] types, with no assignment anywhere in the class. Unity guarantees the
        // component's presence on the GameObject (or throws attaching it), so a GetComponent<T>()
        // fetch here can never legitimately return null.
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Unwired [RequireComponent] field",
            "'{0}' matches a [RequireComponent(typeof({1}))] on '{2}' but is never assigned — "
            + "fetch it with GetComponent<{1}>() in Awake/Start/Reset",
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "[RequireComponent] guarantees the component exists on the GameObject at "
            + "runtime, but does not wire it to a field. Only public and [SerializeField] private "
            + "fields are considered — a field could otherwise be inspector-wired by design, and "
            + "this rule stays silent rather than guess.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (type.TypeKind != TypeKind.Class || type.IsAbstract)
                return;
            if (!InheritsFromMonoBehaviour(type))
                return;

            var requiredTypes = GetRequireComponentTypes(type);
            if (requiredTypes.Length == 0)
                return;

            foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.IsStatic || field.IsConst || field.IsReadOnly)
                    continue;
                if (!IsPublic(field) && !HasSerializeField(field))
                    continue;

                var requiredMatch = requiredTypes.FirstOrDefault(t =>
                    SymbolEqualityComparer.Default.Equals(t, field.Type));
                if (requiredMatch == null)
                    continue;

                if (IsAssignedAnywhere(type, field))
                    continue;

                var syntax = field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                var location = syntax?.GetLocation() ?? type.Locations.FirstOrDefault();
                if (location == null)
                    continue;

                var properties = ImmutableDictionary<string, string?>.Empty
                    .Add("fieldName", field.Name)
                    .Add("typeName", requiredMatch.ToDisplayString());

                context.ReportDiagnostic(Diagnostic.Create(
                    Rule, location, properties, field.Name, requiredMatch.Name, type.Name));
            }
        }

        private static bool InheritsFromMonoBehaviour(INamedTypeSymbol type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current.Name == "MonoBehaviour" &&
                    current.ContainingNamespace?.ToDisplayString() == "UnityEngine")
                    return true;
            }
            return false;
        }

        private static bool IsPublic(IFieldSymbol field) => field.DeclaredAccessibility == Accessibility.Public;

        private static bool HasSerializeField(IFieldSymbol field) =>
            field.GetAttributes().Any(a =>
                a.AttributeClass?.Name == "SerializeFieldAttribute" &&
                a.AttributeClass.ContainingNamespace?.ToDisplayString() == "UnityEngine");

        private static ImmutableArray<ITypeSymbol> GetRequireComponentTypes(INamedTypeSymbol type)
        {
            var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();
            foreach (var attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass?.Name != "RequireComponentAttribute" ||
                    attribute.AttributeClass.ContainingNamespace?.ToDisplayString() != "UnityEngine")
                    continue;

                foreach (var arg in attribute.ConstructorArguments)
                {
                    if (arg.Kind == TypedConstantKind.Type && arg.Value is ITypeSymbol requiredType)
                        builder.Add(requiredType);
                }
            }
            return builder.ToImmutable();
        }

        // True if the field is assigned anywhere in the class: a simple assignment (`field = ...`),
        // a GetComponent<T>() result assigned to it, or an inline initializer. A field wired through
        // any of these is considered handled; the rule only wants the truly untouched case.
        private static bool IsAssignedAnywhere(INamedTypeSymbol type, IFieldSymbol field)
        {
            foreach (var declRef in field.DeclaringSyntaxReferences)
            {
                if (declRef.GetSyntax() is VariableDeclaratorSyntax { Initializer: not null })
                    return true;
            }

            foreach (var typeRef in type.DeclaringSyntaxReferences)
            {
                var syntax = typeRef.GetSyntax();
                foreach (var assignment in syntax.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    if (assignment.Left is IdentifierNameSyntax identifier &&
                        identifier.Identifier.Text == field.Name)
                        return true;
                    if (assignment.Left is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } memberAccess &&
                        memberAccess.Name.Identifier.Text == field.Name)
                        return true;
                }
            }

            return false;
        }
    }
}
