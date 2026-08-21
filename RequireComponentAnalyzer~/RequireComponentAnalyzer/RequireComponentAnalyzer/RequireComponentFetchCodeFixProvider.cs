using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace RequireComponentAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RequireComponentFetchCodeFixProvider)), Shared]
    public class RequireComponentFetchCodeFixProvider : CodeFixProvider
    {
        private static readonly string[] LifecycleMethods = { "Awake", "Start", "Reset" };

        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(RequireComponentFetchAnalyzer.DiagnosticId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            if (!diagnostic.Properties.TryGetValue("fieldName", out var fieldName) ||
                !diagnostic.Properties.TryGetValue("typeName", out var typeName))
                return;

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var classDecl = root!.FindToken(diagnostic.Location.SourceSpan.Start).Parent
                .AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl == null)
                return;

            foreach (var methodName in LifecycleMethods)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Fetch in {methodName}()",
                        createChangedDocument: ct => AddFetchAsync(
                            context.Document, classDecl, methodName, fieldName, typeName, ct),
                        equivalenceKey: RequireComponentFetchAnalyzer.DiagnosticId + "." + methodName),
                    diagnostic);
            }
        }

        private static async Task<Document> AddFetchAsync(
            Document document,
            ClassDeclarationSyntax classDecl,
            string methodName,
            string fieldName,
            string typeName,
            CancellationToken cancellationToken)
        {
            var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

            var fetchStatement = SyntaxFactory.ParseStatement(
                    $"{fieldName} = GetComponent<{typeName}>();")
                .WithAdditionalAnnotations(Simplifier.Annotation);

            var existingMethod = classDecl.Members.OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName && m.ParameterList.Parameters.Count == 0);

            ClassDeclarationSyntax newClassDecl;
            if (existingMethod != null)
            {
                var body = existingMethod.Body ?? SyntaxFactory.Block();
                var newBody = body.AddStatements(fetchStatement).WithAdditionalAnnotations(Formatter.Annotation);
                var newMethod = existingMethod.WithBody(newBody).WithExpressionBody(null)
                    .WithSemicolonToken(default);
                newClassDecl = classDecl.ReplaceNode(existingMethod, newMethod);
            }
            else
            {
                var newMethod = SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                        methodName)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                    .WithBody(SyntaxFactory.Block(fetchStatement))
                    .WithAdditionalAnnotations(Formatter.Annotation);
                newClassDecl = classDecl.AddMembers(newMethod);
            }

            var newRoot = root.ReplaceNode(classDecl, newClassDecl);
            var newDocument = document.WithSyntaxRoot(newRoot);
            return await Simplifier.ReduceAsync(newDocument, Simplifier.Annotation, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
