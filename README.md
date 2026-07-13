![Latest version](https://img.shields.io/nuget/v/HtmlToOpenXml.dll.svg)
![Download Counts](https://img.shields.io/nuget/dt/HtmlToOpenXml.dll.svg)
[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/onizet/html2openxml/blob/dev/LICENSE)

> This is a Briefpoint fork of [onizet/html2openxml](https://github.com/onizet/html2openxml). See [Briefpoint Fork Changes](#briefpoint-fork-changes) below for what diverges from upstream.

# What is HtmlToOpenXml?

HtmlToOpenXml is a small .Net library that convert simple or advanced HTML to plain OpenXml components.

This library supports both **.Net Framework 4.6.2**, **.NET Standard 2.0**, **.NET 8** **.NET 10** which are all LTS.

Depends on [DocumentFormat.OpenXml](https://www.nuget.org/packages/DocumentFormat.OpenXml/) and [AngleSharp](https://www.nuget.org/packages/AngleSharp).

-> [Official Nuget Package](https://www.nuget.org/packages/HtmlToOpenXml.dll)

## See Also

* [Documentation](https://github.com/onizet/html2openxml/wiki)
* [How to deliver a generated DOCX from server Asp.Net/SharePoint?](https://github.com/onizet/html2openxml/wiki/Serves-a-generated-docx-from-the-server)
* [Prevent Document Edition](https://github.com/onizet/html2openxml/wiki/Prevent-Document-Edition)
* [Convert dotx to docx](https://github.com/onizet/html2openxml/wiki/Convert-.dotx-to-.docx)

## Supported Html tags

Refer to [w3schools’ tag](http://www.w3schools.com/tags/default.asp) list to see their meaning

* `a`
* `h1-h6`
* `abbr` and `acronym`
* `b`, `i`, `u`, `s`, `del`, `ins`, `em`, `strike`, `strong`
* `br` and `hr`
* `img`, `figcaption` and `svg`
* `table`, `td`, `tr`, `th`, `tbody`, `thead`, `tfoot`, `caption` and `col`
* `cite`
* `div`, `span`, `time`, `font` and `p`
* `pre`
* `sub` and `sup`
* `ul`, `ol` and `li`
* `dd` and `dt`
* `q`, `blockquote`, `dfn`
* `article`, `aside`, `section` are considered like `div`

Javascript (`script`), CSS `style`, `meta`, comments, buttons and input controls are ignored.
Other tags are treated like `div`.

In v1 and v2, Javascript (`script`), CSS `style`, `meta`, comments and other not supported tags does not generate an error but are **ignored**.

## Html Parser

In v3, the parsing of the Html relies on AngleSharp package, which follows the W3C specifications and actively supports Html5.

In v1 and v2, the parsing of the Html was done using a custom Regex-based enumerator and was more flexible, but leaving a complex code, hard to maintain.

## How to implement or debug features

Reference material covering both OpenXml and HTML:

* [MDN](https://developer.mozilla.org/en-US/docs/Web/HTML)
* [W3Schools](https://www.w3schools.com/html/default.asp)
* [OpenXml MSDN](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing?view=openxml-3.0.1)

Open MS Word or Apple Pages and design your expected output. Save as a DOCX file, then rename as a ZIP. Extract the content and inspect those files:
`document.xml`, `numbering.xml` (for list) and `styles.xml`.

## Briefpoint Fork Changes

This fork carries a small set of behavior changes on top of upstream, made to match Briefpoint's document output requirements:

* **`<mark>` highlight colors** — `<mark>` now maps to a real Word highlight (`w:highlight`) instead of a shading fill, and `highlight-*` CSS classes (`highlight-yellow`, `highlight-red`, `highlight-blue`, `highlight-green`, `highlight-pink`) map to the corresponding Word highlight color, so colors chosen in the response editor survive into the generated docx.
* **Default paragraph style on implicit root paragraphs** — restores the v2 `DefaultStyles.ParagraphStyle` behavior (dropped in the v3 rewrite): loose inline content at the root of the parsed fragment gets wrapped in a paragraph stamped with the default style, without cascading that style onto every descendant paragraph.
* **List item indentation inherits from the list's paragraph style** — when the resolved list item style defines a left/hanging indent, it's applied as direct formatting on each `li` paragraph (with a per-nesting-level offset), overriding the numbering level's indentation instead of using it. Ports a v2-era patch forward into the v3 expression pipeline.
