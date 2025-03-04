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
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HtmlToOpenXml.Expressions;

/// <summary>
/// Top parent expression, processing the <c>body</c> tag,
/// even if it is not directly specified in the provided Html.
/// </summary>
sealed class BodyExpression(IHtmlElement node) : BlockElementExpression(node)
{
    public override IEnumerable<OpenXmlElement> Interpret(ParsingContext context)
    {
        MarkAllBookmarks();

        return base.Interpret(context);
    }

    protected override void ComposeStyles(ParsingContext context)
    {
        base.ComposeStyles(context);

        // Unsupported W3C attribute but claimed by users. Specified at <body> level, the page
        // orientation is applied on the whole document
        string? attr = styleAttributes!["page-orientation"];
        if (attr != null)
        {
            PageOrientationValues orientation = Converter.ToPageOrientation(attr);

            var sectionProperties = context.MainPart.Document.Body!.GetFirstChild<SectionProperties>();
            if (sectionProperties == null || sectionProperties.GetFirstChild<PageSize>() == null)
            {
                context.MainPart.Document.Body.Append(ChangePageOrientation(orientation));
            }
            else
            {
                var pageSize = sectionProperties.GetFirstChild<PageSize>();
                if (pageSize == null || !pageSize.Compare(orientation))
                {
                    SectionProperties validSectionProp = ChangePageOrientation(orientation);
                    pageSize?.Remove();
                    sectionProperties.PrependChild(validSectionProp.GetFirstChild<PageSize>()!.CloneNode(true));
                }
            }
        }

        if (paraProperties.BiDi is not null)
        {
            var sectionProperties = context.MainPart.Document.Body!.GetFirstChild<SectionProperties>();
            if (sectionProperties == null || sectionProperties.GetFirstChild<PageSize>() == null)
            {
                context.MainPart.Document.Body.Append(sectionProperties = new());
            }

            sectionProperties.AddChild(paraProperties.BiDi.CloneNode(true));
        }
    }

    /// <summary>
    /// Generate the required OpenXml element for handling page orientation.
    /// </summary>
    private static SectionProperties ChangePageOrientation(PageOrientationValues orientation)
    {
        PageSize pageSize = new() { Width = (UInt32Value) 16838U, Height = (UInt32Value) 11906U };
        if (orientation == PageOrientationValues.Portrait)
        {
            (pageSize.Height, pageSize.Width) = (pageSize.Width, pageSize.Height);
        }
        else
        {
            pageSize.Orient = orientation;
        }

        return new SectionProperties (
            pageSize,
            new PageMargin() {
                Top = 1417, Right = (UInt32Value) 1417U, Bottom = 1417, Left = (UInt32Value) 1417U,
                Header = (UInt32Value) 708U, Footer = (UInt32Value) 708U, Gutter = (UInt32Value) 0U
            },
            new Columns() { Space = "708" },
            new DocGrid() { LinePitch = 360 }
        );
    }

    /// <summary>
    /// Detect all bookmarks (in-document) and mark the nodes for future processing.
    /// </summary>
    private void MarkAllBookmarks()
    {
        var links = node.QuerySelectorAll("a[href^='#']");
        if (links.Length == 0) return;

        foreach (var link in links.Cast<IHtmlAnchorElement>().Where(l => l.Hash.Length > 0))
        {
            var id = link.Hash.Substring(1);
            var target = node.Owner!.GetElementById(id);
            // `id` attribute is preferred but `name` is also valid
            target ??= node.Owner!.GetElementsByName(id).FirstOrDefault();

            if (target is null) continue;

            if (target.ParentElement is IHtmlHeadingElement heading)
                target = heading;

            // we will be able to retrieve the target during the processing
            target.Attributes.SetNamedItemWithNamespaceUri(
                new Attr("h2ox", "bookmark", id, InternalNamespaceUri));
        }
    }
}