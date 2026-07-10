using NUnit.Framework;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HtmlToOpenXml.Tests
{
    /// <summary>
    /// BP addition: verifies &lt;mark&gt; renders as a Word highlight and that Briefpoint
    /// highlight-* CSS classes map to the expected highlight colors (see MarkHighlightColorMap).
    /// </summary>
    [TestFixture]
    public class MarkHighlightTests : HtmlConverterTestBase
    {
        // Expected values are the serialized OpenXml highlight tokens (HighlightColorValues is a
        // struct, so it cannot be passed as a TestCase attribute constant).
        [TestCase("highlight-yellow", "yellow")]
        [TestCase("highlight-red", "red")]
        [TestCase("highlight-blue", "blue")]
        [TestCase("highlight-green", "green")]
        [TestCase("highlight-pink", "magenta")]
        public void Mark_WithHighlightClass_MapsToWordHighlightColor(string className, string expectedToken)
        {
            var runProperties = ParseMarkRunProperties($@"<mark class=""{className}"">highlighted</mark>");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(runProperties.Highlight?.Val?.InnerText, Is.EqualTo(expectedToken));
                Assert.That(runProperties.Shading, Is.Null);
            }
        }

        [TestCase(@"<mark>highlighted</mark>")]
        [TestCase(@"<mark class=""unknown-class"">highlighted</mark>")]
        public void Mark_WithoutRecognizedClass_DefaultsToYellow(string html)
        {
            var runProperties = ParseMarkRunProperties(html);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(runProperties.Highlight?.Val?.InnerText, Is.EqualTo("yellow"));
                Assert.That(runProperties.Shading, Is.Null);
            }
        }

        private RunProperties ParseMarkRunProperties(string html)
        {
            var elements = converter.Parse(html);
            Assert.That(elements, Has.Count.EqualTo(1));

            var run = elements[0].GetFirstChild<Run>();
            Assert.That(run, Is.Not.Null);

            var runProperties = run.GetFirstChild<RunProperties>();
            Assert.That(runProperties, Is.Not.Null);
            return runProperties;
        }
    }
}
