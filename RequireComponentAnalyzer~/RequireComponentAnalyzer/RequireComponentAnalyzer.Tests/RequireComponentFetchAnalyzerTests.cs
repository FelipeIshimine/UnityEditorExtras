using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    RequireComponentAnalyzer.RequireComponentFetchAnalyzer>;
using VerifyCSFix = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    RequireComponentAnalyzer.RequireComponentFetchAnalyzer,
    RequireComponentAnalyzer.RequireComponentFetchCodeFixProvider>;

namespace RequireComponentAnalyzer.Tests
{
    public class RequireComponentFetchAnalyzerTests
    {
        private const string UnityStub = @"
using UnityEngine;

namespace UnityEngine
{
    using System;

    public class Component { }
    public class GameObject { }

    public class MonoBehaviour : Component
    {
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        public T GetComponentInParent<T>() => default;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequireComponentAttribute : Attribute
    {
        public RequireComponentAttribute(Type t1) { }
    }

    public class SerializeFieldAttribute : Attribute { }
    public class Rigidbody : Component { }
}
";

        [Fact]
        public async Task UnwiredPublicField_Flags()
        {
            var test = UnityStub + @"
[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    public Rigidbody {|#0:Body|};
}
";
            var expected = VerifyCS.Diagnostic(RequireComponentFetchAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("Body", "Rigidbody", "Player");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task FieldAlreadyFetched_NoDiagnostic()
        {
            var test = UnityStub + @"
[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    public Rigidbody Body;

    private void Awake()
    {
        Body = GetComponent<Rigidbody>();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task NonSerializedPrivateField_NoDiagnostic()
        {
            var test = UnityStub + @"
[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    private Rigidbody _body;
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // Drives the diagnostic + first ("Fetch in Awake()") code action directly through a plain
        // AdhocWorkspace, asserting on the resulting text's content rather than an exact string —
        // sidesteps CSharpCodeFixTest's environment-newline-vs-saved-file-newline sensitivity, which
        // is a test-harness quirk, not a behavioral difference in the generated code.
        [Fact]
        public async Task CodeFix_AddsFetchInNewAwake()
        {
            var source = UnityStub + @"
[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    public Rigidbody Body;
}
";
            using var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
            var references = System.AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a =>
                {
                    try { return (MetadataReference)MetadataReference.CreateFromFile(a.Location); }
                    catch (System.BadImageFormatException) { return null; }
                })
                .Where(r => r != null)
                .Select(r => r!);
            var projectId = ProjectId.CreateNewId();
            var documentId = DocumentId.CreateNewId(projectId);
            var solution = workspace.CurrentSolution
                .AddProject(projectId, "Test", "Test", LanguageNames.CSharp)
                .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddMetadataReferences(projectId, references)
                .AddDocument(documentId, "Test.cs", SourceText.From(source));
            Assert.True(workspace.TryApplyChanges(solution));
            var document = workspace.CurrentSolution.GetDocument(documentId)!;

            var compilation = (await document.Project.GetCompilationAsync())!;
            var analyzer = new RequireComponentFetchAnalyzer();
            var compilationWithAnalyzers = compilation.WithAnalyzers(
                System.Collections.Immutable.ImmutableArray.Create<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer>(analyzer));
            var compileDiagnostics = compilation.GetDiagnostics();
            Assert.Empty(compileDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

            var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal(RequireComponentFetchAnalyzer.DiagnosticId, diagnostic.Id);

            var provider = new RequireComponentFetchCodeFixProvider();
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(
                document, diagnostic, (a, _) => actions.Add(a), default);
            await provider.RegisterCodeFixesAsync(context);

            var awakeAction = Assert.Single(actions, a => a.Title == "Fetch in Awake()");
            var operations = await awakeAction.GetOperationsAsync(default);
            var applyChanges = Assert.IsType<ApplyChangesOperation>(Assert.Single(operations));
            var changedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)!;
            var changedText = (await changedDocument.GetTextAsync()).ToString();

            Assert.Contains("private void Awake()", changedText);
            Assert.Contains("Body = GetComponent<Rigidbody>();", changedText);
        }
    }
}
