/* Copyright (C) Olivier Nizet https://github.com/onizet/html2openxml - All Rights Reserved
 * 
 * This source is subject to the Microsoft Permissive License.
 * Please see the License.txt file for more information.
 * All other rights reserved.
 * 
 * THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY 
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
 * PARTICULAR PURPOSE.
 */
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HtmlToOpenXml.Expressions;

/// <summary>
/// Process the ordered <c>ol</c> and unordered <c>ul</c> list.
/// </summary>
sealed class ListExpression(IHtmlElement node) : NumberingExpressionBase(node)
{
#if NET5_0_OR_GREATER
    readonly record struct ListContext(string Name, int AbsNumId, int InstanceId, int Level, DirectionMode? Dir);
#else
    readonly struct ListContext(string listName, int absNumId, int instanceId, int level, DirectionMode? dir)
    {
        public readonly string Name = listName;
        public readonly int AbsNumId = absNumId;
        public readonly int InstanceId = instanceId;
        public readonly int Level = level;
        public readonly DirectionMode? Dir = dir;
    }
#endif

    // https://www.w3schools.com/cssref/playdemo.php?filename=playcss_list-style-type
    // https://answers.microsoft.com/en-us/msoffice/forum/all/custom-list-number-style/21a54399-4404-4c37-8843-2ccaaf827485
    // Image bullet: http://officeopenxml.com/WPnumbering-imagesAsSymbol.php
    private static readonly HashSet<string> supportedListTypes = 
        ["disc", "decimal", "square", "circle",
         "lower-alpha", "upper-alpha", "lower-latin", "upper-latin",
         "lower-greek", "upper-greek",
         "lower-roman", "upper-roman",
         "decimal-tiered" /* not W3C compliant */];
    private ParagraphStyleId? listParagraphStyleId;


    public override IEnumerable<OpenXmlElement> Interpret(ParsingContext context)
    {
        var liNodes = node.Children.Where(n => n.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase));
        if (!liNodes.Any()) yield break;

        // W3C requires that nested list stands below a `li` element but some editors
        // don't care to respect the standard. Let's reparent those lists
        var nestedList = node.Children.Where(n => 
            n.LocalName.Equals("ol", StringComparison.OrdinalIgnoreCase) || 
            n.LocalName.Equals("ul", StringComparison.OrdinalIgnoreCase));
        if (nestedList.Any())
        {
            foreach (var list in nestedList)
                list.PreviousElementSibling?.AppendChild(list);
        }

        var listContext = context.Properties<ListContext>("listContext");
        var parentContext = listContext;
        var listStyle = GetListName(node, listContext.Name);
        if (listContext.InstanceId == 0 || listContext.Name != listStyle)
        {
            var abstractNumId = GetOrCreateListTemplate(context, listStyle);
            listContext = ConcretiseInstance(context, abstractNumId, listStyle, listContext.Level);

            listParagraphStyleId = GetStyleIdForListItem(context.DocumentStyle, node, defaultIfEmpty: false);
        }
        else
        {
            var dir = node.GetTextDirection();
            listContext = new ListContext(listContext.Name, listContext.AbsNumId, 
                listContext.InstanceId, listContext.Level + 1, dir ?? listContext.Dir);
        }

        context.Properties("listContext", listContext);

        // +1 because index starts on 1 and not 0
        var level = Math.Min(listContext.Level, MaxLevel+1);

        // resolve the indentation inherited from the ancestors (eg, a parent with margin-left),
        // as the list own indentation must be applied in addition to it
        var ancestorIndent = context.Properties<int>("listIndent");
        var probe = new Paragraph();
        context.CascadeStyles(probe);
        int.TryParse(probe.ParagraphProperties?.Indentation?.Left?.Value, out int inheritedIndent);

        // HTML5 parsing auto-closes a `p` when a list begins, so a list authored inside
        // an indented paragraph ends up as its sibling, leaving behind an empty `p` which
        // produces no output. Recover its indentation as the inherited one
        if (inheritedIndent == 0
            && node.PreviousElementSibling is IHtmlElement prevParagraph
            && prevParagraph.LocalName.Equals("p", StringComparison.OrdinalIgnoreCase)
            && prevParagraph.ChildElementCount == 0
            && string.IsNullOrWhiteSpace(prevParagraph.TextContent))
        {
            var margin = prevParagraph.GetStyles().GetMargin("margin");
            if (margin.Left.IsFixed && margin.Left.Value > 0)
                inheritedIndent = (int) margin.Left.ValueInDxa;
        }

        inheritedIndent = Math.Max(inheritedIndent, ancestorIndent);
        context.Properties("listIndent", inheritedIndent);

        foreach (IHtmlElement liNode in liNodes.Cast<IHtmlElement>())
        {
            var expression = new BlockElementExpression(liNode);
            var childElements = expression.Interpret(context);
            if (!childElements.Any()) continue;

            // table must be aligned to the list item
            var tables = childElements.OfType<Table>();
            var tableIndentation = level * Indentation;
            foreach (var table in tables)
            {
                var tableProperties = table.GetFirstChild<TableProperties>();
                if (tableProperties == null)
                    table.PrependChild(tableProperties = new());

                tableProperties.TableIndentation ??= new() { Width = tableIndentation * 2 + inheritedIndent };
                // ensure to restrain the table width to the list item
                if (tableProperties.TableWidth?.Type?.Value == TableWidthUnitValues.Pct
                    && tableProperties.TableWidth?.Width?.Value == "5000")
                {
                    tableProperties.TableWidth.Width = (5000 - tableIndentation).ToString();
                }
            }

            // ensure to filter out any non-paragraph like any nested table
            var paragraphs = childElements.OfType<Paragraph>();
            var listItemStyleId = GetStyleIdForListItem(context.DocumentStyle, liNode);

            if (paragraphs.Any())
            {
                var p = paragraphs.First();
                p.ParagraphProperties ??= new();
                p.ParagraphProperties.ParagraphStyleId = listItemStyleId;
                p.ParagraphProperties!.NumberingProperties ??= new NumberingProperties {
                    NumberingLevelReference = new() { Val = level - 1 },
                    NumberingId = new() { Val = listContext.InstanceId }
                };
                if (listContext.Dir.HasValue) {
                    p.ParagraphProperties.BiDi = new() {
                        Val = OnOffValue.FromBoolean(listContext.Dir == DirectionMode.Rtl)
                    };
                }

                // the indentation cascaded from the ancestors would override the numbering
                // level indentation, so combine both to keep the hierarchical indent
                var indentation = p.ParagraphProperties.Indentation;
                int.TryParse(indentation?.Left?.Value, out int paraIndent);
                if (paraIndent == 0) paraIndent = inheritedIndent;
                if (paraIndent != 0)
                {
                    indentation ??= p.ParagraphProperties.Indentation = new();
                    indentation.Left = (paraIndent + level * Indentation * 2).ToString();
                    indentation.Hanging ??= Indentation.ToString();
                }
            }

            // any standalone paragraphs must be aligned (indented) along its current level
            foreach (var p in paragraphs.Skip(1))
            {
                // if this is a list item paragraph, skip it
                if (p.ParagraphProperties?.NumberingProperties is not null)
                    continue;

                p.ParagraphProperties ??= new();
                p.ParagraphProperties.ParagraphStyleId ??= (ParagraphStyleId?) listItemStyleId!.CloneNode(true);
                int.TryParse(p.ParagraphProperties.Indentation?.Left?.Value, out int standaloneIndent);
                if (standaloneIndent == 0) standaloneIndent = inheritedIndent;
                p.ParagraphProperties.Indentation = new() {
                    Left = (standaloneIndent + level * Indentation * 2).ToString()
                };
            }

            foreach (var child in childElements)
                yield return child;
        }

        context.Properties("listContext", parentContext);
        context.Properties("listIndent", ancestorIndent);
    }

    /// <summary>
    /// Create a new instance of a list template.
    /// </summary>
    private ListContext ConcretiseInstance(ParsingContext context, int abstractNumId, string listStyle, int currentLevel)
    {
        ListContext listContext;

        var instanceId = GetListInstance(abstractNumId);
        int overrideLevelIndex = 0;
        var isOrderedTag = node.NodeName.Equals("ol", StringComparison.OrdinalIgnoreCase);
        var dir = node.GetTextDirection();

        // be sure to restart to 1 any nested ordered list
        if (currentLevel > 0 && isOrderedTag)
        {
            instanceId = IncrementInstanceId(context, abstractNumId, isReusable: false);
            overrideLevelIndex = currentLevel;
            listContext = new ListContext(listStyle, abstractNumId, instanceId.Value, currentLevel + 1, dir);
        }
        else if (!instanceId.HasValue || context.Converter.ContinueNumbering == false)
        {
            // create a new instance of that list template
            instanceId = IncrementInstanceId(context, abstractNumId, isReusable: context.Converter.ContinueNumbering);
            listContext = new ListContext(listStyle, abstractNumId, instanceId.Value, currentLevel + 1, dir);
        }
        else
            // if the previous element is the same list style,
            // we must restart the ordering to 0
            if (node.IsPrecededByListElement(out var precedingElement)
                && GetListName(precedingElement!) == listStyle)
        {
            instanceId = IncrementInstanceId(context, abstractNumId, isReusable: false);
            listContext =  new ListContext(listStyle, abstractNumId, instanceId.Value, 1, dir);
        }
        else
        {
            return new ListContext(listStyle, abstractNumId, instanceId.Value, currentLevel + 1, dir);
        }

        int startValue = 1;
        if (isOrderedTag)
        {
            var startAttribute = node.GetAttribute("start");
            if (startAttribute != null && int.TryParse(startAttribute, out var val) && val > 1)
                startValue = val;
        }

        var numbering = context.MainPart.NumberingDefinitionsPart!.Numbering!;
        numbering.Append(
            new NumberingInstance(
                new AbstractNumId() { Val = abstractNumId },
                new LevelOverride(
                    new StartOverrideNumberingValue() { Val = startValue }
                ) { LevelIndex = overrideLevelIndex }
            )
            { NumberID = instanceId.Value });

        return listContext;
    }

    /// <summary>
    /// Resolve the list style to determine which NumberList style to apply.
    /// </summary>
    private static string GetListName(IElement listNode, string? parentName = null)
    {
        var styleAttributes = listNode.GetStyles();
        bool orderedList = listNode.NodeName.Equals("ol", StringComparison.OrdinalIgnoreCase);
        string? type = styleAttributes["list-style-type"].ToString();

        if(orderedList && string.IsNullOrEmpty(type))
        {
            type = ListTypeToListStyleType(listNode.GetAttribute("type"));
        }

        if (string.IsNullOrEmpty(type) || !supportedListTypes.Contains(type!))
        {
            if (parentName != null && IsCascadingStyle(parentName))
                return parentName!;

            type = orderedList? "decimal" : "disc";
        }

        return type!;
    }

    /// <summary>
    /// Map ordered list style attribute values to css list-style-type.
    /// Valid types are "1|a|A|i|I": https://w3schools.com/tags/att_ol_type.asp
    /// </summary>
    private static string? ListTypeToListStyleType(string? type) => type switch
    {
        "1" => "decimal",
        "a" => "lower-alpha",
        "A" => "upper-alpha",
        "i" => "lower-roman",
        "I" => "upper-roman",
        "lower-greek" or "upper-greek" => type,
        _ => null
    };

    /// <summary>
    /// Resolve the <see cref="ParagraphStyleId"/> of a list element node, 
    /// based on its css class if provided and if matching.
    /// </summary>
    private ParagraphStyleId? GetStyleIdForListItem(WordDocumentStyle documentStyle, IHtmlElement liNode, bool defaultIfEmpty = true)
    {
        if (listParagraphStyleId != null)
            return (ParagraphStyleId) listParagraphStyleId.Clone();

        foreach(var clsName in liNode.ClassList)
        {
            var styleId = documentStyle.GetStyle(clsName, StyleValues.Paragraph, ignoreCase: true);
            if (styleId != null)
                return new ParagraphStyleId { Val = styleId };
        }

        if (!defaultIfEmpty) return null;
        return documentStyle.GetParagraphStyle(documentStyle.DefaultStyles.ListParagraphStyle);
    }

    /// <summary>
    /// Gets whether the given style is automatically promoted to child lists.
    /// </summary>
    private static bool IsCascadingStyle(string styleName)
    {
        return styleName == "decimal-tiered";
    }
}
