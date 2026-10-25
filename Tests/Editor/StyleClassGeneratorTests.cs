using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace UnityEditorExtras.Editor.Tests
{
    public sealed class StyleClassGeneratorTests
    {
        [Test]
        public void Classes_AreReadFromSelectorsOnly()
        {
            const string uss = @"
.card { background-image: url(""project://database/Assets/Art/card.png?x=1#card""); }
.card__title, .card--short .card__title:hover { -unity-font-definition: url(""Fonts/Bold.asset""); }
/* .commented { } */
";

            CollectionAssert.AreEquivalent(
                new[] { "card", "card__title", "card--short" },
                StyleClassGenerator.ClassesIn(uss).ToArray());
        }

        [TestCase("unit-card", "UnitCard")]
        [TestCase("unit-card__title-text", "UnitCardTitleText")]
        [TestCase("unit-card--short", "UnitCard_Short")]
        [TestCase("highlight--candidate", "Highlight_Candidate")]
        [TestCase("stat-area__text--big", "StatAreaText_Big")]
        public void Identifier_IsPascalCase_WithModifiersAfterAnUnderscore(string ussClass, string identifier)
        {
            Assert.AreEqual(identifier, StyleClassGenerator.IdentifierOf(ussClass));
        }

        [Test]
        public void SameClassInTwoSheets_IsOneMember()
        {
            var sheets = new Dictionary<string, string> { ["a.uss"] = ".card { }", ["b.uss"] = ".card { }" };

            Assert.AreEqual(1, StyleClassGenerator.Members("Style", sheets).Count);
        }

        [Test]
        public void TwoClassesWithOneIdentifier_Throw()
        {
            var sheets = new Dictionary<string, string> { ["a.uss"] = ".unit-card { } .unit_card { }" };

            Assert.Throws<InvalidOperationException>(() => StyleClassGenerator.Members("Style", sheets));
        }

        [Test]
        public void ClassNamedLikeTheGeneratedClass_Throws()
        {
            var sheets = new Dictionary<string, string> { ["a.uss"] = ".style { }" };

            Assert.Throws<InvalidOperationException>(() => StyleClassGenerator.Members("Style", sheets));
        }

        [Test]
        public void Emit_WritesOneConstantPerClass()
        {
            var members = StyleClassGenerator.Members("Style", new Dictionary<string, string> { ["a.uss"] = ".card--short { }" });

            StringAssert.Contains("public const string Card_Short = \"card--short\";",
                StyleClassGenerator.Emit("Style", "Game.UI", members));
        }
    }
}
