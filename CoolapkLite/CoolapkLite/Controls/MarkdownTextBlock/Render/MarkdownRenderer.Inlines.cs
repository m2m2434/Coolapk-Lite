// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CoolapkLite.Parsers.Markdown;
using CoolapkLite.Parsers.Markdown.Inlines;
using CoolapkLite.Parsers.Markdown.Render;
using System;
using System.Collections.Generic;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace CoolapkLite.Controls.Render
{
    /// <summary>
    /// Inline UI Methods for UWP UI Creation.
    /// </summary>
    public partial class MarkdownRenderer
    {
#pragma warning disable CS0618 // Type or member is obsolete
        /// <summary>
        /// Renders emoji element.
        /// </summary>
        /// <param name="element"> The parsed inline element to render. </param>
        /// <param name="context"> Persistent state. </param>
        protected override void RenderEmoji(EmojiInline element, IRenderContext context)
        {
            InlineRenderContext localContext = context as InlineRenderContext;
            if (localContext == null)
            {
                throw new RenderContextIncorrectException();
            }

            InlineCollection inlineCollection = localContext.InlineCollection;

            Run emoji = new Run
            {
                FontFamily = EmojiFontFamily ?? DefaultEmojiFont,
                Text = element.Text
            };

            inlineCollection.Add(emoji);
        }

        /// <summary>
        /// Renders a text run element.
        /// </summary>
        /// <param name="element"> The parsed inline element to render. </param>
        /// <param name="context"> Persistent state. </param>
        protected override void RenderTextRun(TextRunInline element, IRenderContext context)
        {
            InternalRenderTextRun(element, context);
        }

        private Run InternalRenderTextRun(TextRunInline element, IRenderContext context)
        {
            InlineRenderContext localContext = context as InlineRenderContext;
            if (localContext == null)
            {
                throw new RenderContextIncorrectException();
            }

            InlineCollection inlineCollection = localContext.InlineCollection;

            // Create the text run
            Run textRun = new Run
            {
                Text = CollapseWhitespace(context, element.Text)
            };

            // Add it
            inlineCollection.Add(textRun);
            return textRun;
        }

        /// <summary>
        /// Renders a bold run element.
        /// </summary>
        /// <param name="element"> The parsed inline element to render. </param>
        /// <param name="context"> Persistent state. </param>
        protected override void RenderBoldRun(BoldTextInline element, IRenderContext context)
        {
            InlineRenderContext localContext = context as InlineRenderContext;
            if (localContext == null)
            {
                throw new RenderContextIncorrectException();
            }

            // Create the text run
            Span boldSpan = new Span
            {
                FontWeight = FontWeights.Bold
            };

            InlineRenderContext childContext = new InlineRenderContext(boldSpan.Inlines, context)
            {
                Parent = boldSpan,
                WithinBold = true
            };

            // Render the children into the bold inline.
            RenderInlineChildren(element.Inlines, childContext);

            // Add it to the current inlines
            localContext.InlineCollection.Add(boldSpan);
        }

        /// <summary>
        /// Renders a link element
        /// </summary>
        /// <param name="element"> The parsed inline element to render. </param>
        /// <param name="context"> Persistent state. </param>
        protected override void RenderMarkdownLink(MarkdownLinkInline element, IRenderContext context)
        {
            InlineRenderContext localContext = context as InlineRenderContext;
            if (localContext == null)
            {
                throw new RenderContextIncorrectException();
            }

            // HACK: Superscript is not allowed within a hyperlink.  But if we switch it around, so
            // that the superscript is outside the hyperlink, then it will render correctly.
            // This assumes that the entire hyperlink is to be rendered as superscript.
            if (AllTextIsSuperscript(element) == false)
            {
                // Regular ol' hyperlink.
                Hyperlink link = new Hyperlink();

                // Register the link
                LinkRegister.RegisterNewHyperLink(link, element.Url);

                // Remove superscripts.
                RemoveSuperscriptRuns(element, insertCaret: true);

                // Render the children into the link inline.
                InlineRenderContext childContext = new InlineRenderContext(link.Inlines, context)
                {
                    Parent = link,
                    WithinHyperlink = true
                };

                if (localContext.OverrideForeground)
                {
                    link.Foreground = localContext.Foreground;
                }
                else if (LinkForeground != null)
                {
                    link.Foreground = LinkForeground;
                }

                RenderInlineChildren(element.Inlines, childContext);
                context.TrimLeadingWhitespace = childContext.TrimLeadingWhitespace;

                ToolTipService.SetToolTip(link, element.Tooltip ?? element.Url);

                // Add it to the current inlines
                localContext.InlineCollection.Add(link);
            }
            else
            {
                // THE HACK IS ON!

                // Create a fake superscript element.
                SuperscriptTextInline fakeSuperscript = new SuperscriptTextInline
                {
                    Inlines = new List<MarkdownInline>
                    {
                        element
                    }
                };

                // Remove superscripts.
                RemoveSuperscriptRuns(element, insertCaret: false);

                // Now render it.
                RenderSuperscriptRun(fakeSuperscript, context);
            }
        }

        /// <summary>
        /// Renders a raw link element.
        /// </summary>
        /// <param name="element"> The parsed inline element to render. </param>
        /// <param name="context"> Persistent state. </param>
        protected override void RenderHyperlink(HyperlinkInline element, IRenderContext context)
        {
            InlineRenderContext localContext = context as InlineRenderContext;
            if (localContext == null)
            {
                throw new RenderContextIncorrectException();
            }

            Hyperlink link = new Hyperlink();

            // Register the link
            LinkRegister.RegisterNewHyperLink(link, element.Url);

            Brush brush = localContext.Foreground;
            if (LinkForeground != null && !localContext.OverrideForeground)
            {
                brush = LinkForeground;
            }

            // Make a text block for the link
            Run linkText = new Run
            {
                Text = CollapseWhitespace(context, element.Text),
                Foreground = brush
            };

            link.Inlines.Add(linkText);

            // Add it to the current inlines
            localContext.InlineCollection.Add(link);
        }

        /// <summary>
        /// Renders an image element.
        /// </summary>
        /// <param name="element"> The parsed inline element to render. </param>
        /// <param name="context"> Persistent state. </param>
        protected override async void RenderImage(ImageInline element, IRenderContext context)
        {
            InlineRenderContext localContext = context as InlineRenderContext;
            if (localContext == null)
            {
                throw new RenderContextIncorrectException();
            }

            InlineCollection inlineCollection = localContext.InlineCollection;

            Run placeholder = InternalRenderTextRun(new TextRunInline { Text = element.Text, Type = MarkdownInlineType.TextRun }, context);
            ImageSource resolvedImage = await ImageResolver.ResolveImageAsync(element.RenderUrl, element.Tooltip);

            // if image can not be resolved we have to return
            if (resolvedImage == null)
            {
                return;
            }

            Image image = new Image
            {
                Source = resolvedImage,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Stretch = ImageStretch
            };

            HyperlinkButton hyperlinkButton = new HyperlinkButton()
            {
                Content = image
            };

            Viewbox viewbox = new Viewbox
            {
                Child = hyperlinkButton,
                StretchDirection = Windows.UI.Xaml.Controls.StretchDirection.DownOnly
            };

            viewbox.PointerWheelChanged += Preventative_PointerWheelChanged;

            ScrollViewer scrollViewer = new ScrollViewer
            {
                Content = viewbox,
                VerticalScrollMode = ScrollMode.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            InlineUIContainer imageContainer = new InlineUIContainer() { Child = scrollViewer };

            bool ishyperlink = false;
            if (element.RenderUrl != element.Url)
            {
                ishyperlink = true;
            }

            LinkRegister.RegisterNewHyperLink(image, element.Url, ishyperlink);

            if (ImageMaxHeight > 0)
            {
                viewbox.MaxHeight = ImageMaxHeight;
            }

            if (ImageMaxWidth > 0)
            {
                viewbox.MaxWidth = ImageMaxWidth;
            }

            if (element.ImageWidth > 0)
            {
                image.Width = element.ImageWidth;
                image.Stretch = Stretch.UniformToFill;
            }

            if (element.ImageHeight > 0)
            {
                if (element.ImageWidth == 0)
                {
                    image.Width = element.ImageHeight;
                }

                image.Height = element.ImageHeight;
                image.Stretch = Stretch.UniformToFill;
            }

            if (element.ImageHeight > 0 && element.ImageWidth > 0)
            {
                image.Stretch = Stretch.Fill;
            }

            // If image size is given then scroll to view overflown part
            if (element.ImageHeight > 0 || element.ImageWidth > 0)
            {
                scrollViewer.HorizontalScrollMode = ScrollMode.Auto;
                scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            }

            // Else resize the image
            else
            {
                scrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
                scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }

            ToolTipService.SetToolTip(image, element.Tooltip);

            // Try to add it to the current inlines
            // Could fail because some containers like Hyperlink cannot have inlined images
            try
            {
                int placeholderIndex = inlineCollection.IndexOf(placeholder);
                inlineCollection.Remove(placeholder);
                inlineCollection.Insert(placeholderIndex, imageContainer);
            }
            catch
            {
                // Ignore error
            }
        }

        /// <summary>
        /// Renders a text run element.
        /// </summary>
        /// <param name="element"> The parsed inline element to render. </param>
        /// <param name="context"> Persistent state. </param>
        protected override void RenderItalicRun(ItalicTextInline element, IRenderContext context)
        {
            InlineRenderContext localContext = context as InlineRenderContext;
            if (localContext == null)
            {
                throw new RenderContextIncorrectException();
            }

            // Create the text run
            Span italicSpan = new Span
            {
                FontStyle = FontStyle.Italic
            };

            InlineRenderContext childContext = new InlineRenderContext(italicSpan.Inlines, context)
            {
                Parent = italicSpan,
                WithinItalics = true
            };

            // Render the children into the italic inline.
            RenderInlineChildren(element.Inlines, childContext);

            // Add it to the current inlines
            localContext.InlineCollection.Add(italicSpan);
        }

        /// <summary>
        /// Renders a strikethrough element.
        /// </summary>
        /// <param name="element"> The parsed inline element to render. </param>
        /// <param name="context"> Persistent state. </param>
        protected override void RenderStrikethroughRun(StrikethroughTextInline element, IRenderContext context)
        {
            InlineRenderContext localContext = context as InlineRenderContext;
            if (localContext == null)
            {
                throw new RenderContextIncorrectException();
            }

            Span span = new Span();

            span.TextDecorations = TextDecorations.Strikethrough;

            InlineRenderContext childContext = new InlineRenderContext(span.Inlines, context)
            {
                Parent = span
            };

            // Render the children into the inline.
            RenderInlineChildren(element.Inlines, childContext);

            // Add it to the current inlines
            localContext.InlineCollection.Add(span);
        }

        /// <summary>
        /// Renders a superscript element.
        /// </summary>
        /// <param name="element"> The parsed inline element to render. </param>
        /// <param name="context"> Persistent state. </param>
        protected override void RenderSuperscriptRun(SuperscriptTextInline element, IRenderContext context)
        {
            InlineRenderContext localContext = context as InlineRenderContext;
            TextElement parent = localContext?.Parent as TextElement;
            if (localContext == null && parent == null)
            {
                throw new RenderContextIncorrectException();
            }

            // Le <sigh>, InlineUIContainers are not allowed within hyperlinks.
            if (localContext.WithinHyperlink)
            {
                RenderInlineChildren(element.Inlines, context);
                return;
            }

            Paragraph paragraph = new Paragraph
            {
                FontSize = parent.FontSize * 0.8,
                FontFamily = parent.FontFamily,
                FontStyle = parent.FontStyle,
                FontWeight = parent.FontWeight
            };

            InlineRenderContext childContext = new InlineRenderContext(paragraph.Inlines, context)
            {
                Parent = paragraph
            };

            RenderInlineChildren(element.Inlines, childContext);

            RichTextBlock richTextBlock = CreateOrReuseRichTextBlock(new UIElementCollectionRenderContext(null, context));
            richTextBlock.Blocks.Add(paragraph);

            Border border = new Border
            {
                Padding = new Thickness(0, 0, 0, paragraph.FontSize * 0.2),
                Child = richTextBlock
            };

            InlineUIContainer inlineUIContainer = new InlineUIContainer
            {
                Child = border
            };

            // Add it to the current inlines
            localContext.InlineCollection.Add(inlineUIContainer);
        }

        /// <summary>
        /// Renders a subscript element.
        /// </summary>
        /// <param name="element"> The parsed inline element to render. </param>
        /// <param name="context"> Persistent state. </param>
        protected override void RenderSubscriptRun(SubscriptTextInline element, IRenderContext context)
        {
            InlineRenderContext localContext = context as InlineRenderContext;
            TextElement parent = localContext?.Parent as TextElement;
            if (localContext == null && parent == null)
            {
                throw new RenderContextIncorrectException();
            }

            Paragraph paragraph = new Paragraph
            {
                FontSize = parent.FontSize * 0.7,
                FontFamily = parent.FontFamily,
                FontStyle = parent.FontStyle,
                FontWeight = parent.FontWeight
            };

            InlineRenderContext childContext = new InlineRenderContext(paragraph.Inlines, context)
            {
                Parent = paragraph
            };

            RenderInlineChildren(element.Inlines, childContext);

            RichTextBlock richTextBlock = CreateOrReuseRichTextBlock(new UIElementCollectionRenderContext(null, context));
            richTextBlock.Blocks.Add(paragraph);

            Border border = new Border
            {
                Margin = new Thickness(0, 0, 0, (-1) * (paragraph.FontSize * 0.6)),
                Child = richTextBlock
            };

            InlineUIContainer inlineUIContainer = new InlineUIContainer
            {
                Child = border
            };

            // Add it to the current inlines
            localContext.InlineCollection.Add(inlineUIContainer);
        }

        /// <summary>
        /// Renders a code element
        /// </summary>
        /// <param name="element"> The parsed inline element to render. </param>
        /// <param name="context"> Persistent state. </param>
        protected override void RenderCodeRun(CodeInline element, IRenderContext context)
        {
            InlineRenderContext localContext = context as InlineRenderContext;
            if (localContext == null)
            {
                throw new RenderContextIncorrectException();
            }

            string text = CollapseWhitespace(context, element.Text);

            // Avoid a crash if the current inline is inside an hyperline.
            // This happens when using inline code blocks like [`SomeCode`](https://www.foo.bar).
            if (localContext.Parent is Hyperlink)
            {
                // Fallback span
                Run run = new Run
                {
                    Text = text,
                    FontFamily = InlineCodeFontFamily ?? FontFamily,
                    Foreground = InlineCodeForeground ?? Foreground
                };

                // Additional formatting
                if (localContext.WithinItalics)
                {
                    run.FontStyle = FontStyle.Italic;
                }

                if (localContext.WithinBold)
                {
                    run.FontWeight = FontWeights.Bold;
                }

                // Add the fallback block
                localContext.InlineCollection.Add(run);
            }
            else
            {
                TextBlock textBlock = CreateTextBlock(localContext);
                textBlock.Text = text;
                textBlock.FontFamily = InlineCodeFontFamily ?? FontFamily;
                textBlock.Foreground = InlineCodeForeground ?? Foreground;

                if (localContext.WithinItalics)
                {
                    textBlock.FontStyle = FontStyle.Italic;
                }

                if (localContext.WithinBold)
                {
                    textBlock.FontWeight = FontWeights.Bold;
                }

                InlineUIContainer inlineUIContainer = new InlineUIContainer
                {
                    Child = new Border
                    {
                        BorderThickness = InlineCodeBorderThickness,
                        BorderBrush = InlineCodeBorderBrush,
                        Background = InlineCodeBackground,
                        Child = textBlock,
                        Padding = InlineCodePadding,
                        Margin = InlineCodeMargin,

                        // Aligns content in InlineUI, see https://social.msdn.microsoft.com/Forums/silverlight/en-US/48b5e91e-efc5-4768-8eaf-f897849fcf0b/richtextbox-inlineuicontainer-vertical-alignment-issue?forum=silverlightarchieve
                        RenderTransform = new TranslateTransform { Y = 4 }
                    }
                };

                // Add it to the current inlines
                localContext.InlineCollection.Add(inlineUIContainer);
            }
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }
}