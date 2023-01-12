using Markdig;
using Markdig.Extensions.AutoLinks;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Nostrid.Model;
using System.Text.RegularExpressions;

namespace Nostrid.Data
{
    internal partial class NoteProcessor
    {
        private const string LinkRegexText = "https?:\\/\\/(?:www\\.)?[-a-zA-Z0-9@:%._\\+~#=]{1,256}\\.[a-zA-Z0-9()]{1,6}\\b(?:[-a-zA-Z0-9()@:%_\\+.~#?&\\/=]*)";
        private const string NumberRegexText = "\\d+";
        private const string LabelRegexText = "[a-z0-9_]+";
        private static readonly Regex hashtagRegex = HashtagRegex();
        private static readonly Regex partsRegex = PartsRegex();
        private static readonly Regex partsWithLinksRegex = PartsWithLinksRegex();
        private static readonly Regex linkRegex = LinkRegex();
        private static readonly Regex mentionWithIndexRegex = MentionWithIndexRegex();

        private readonly MarkdownPipeline pipeline;

        public NoteProcessor()
        {
            var builder = new MarkdownPipelineBuilder();
            builder.Extensions.Add(new AutoLinkExtension(new AutoLinkOptions() { OpenInNewWindow = true }));
            builder.Extensions.Add(new MyParagraphExtension());
            pipeline = builder.Build();
        }

        private bool HasMarkup(MarkdownObject block)
        {
            switch (block)
            {
                case MarkdownDocument markdownDocument:
                    foreach (var item in markdownDocument)
                        if (HasMarkup(item))
                            return true;
                    return false;
                case ParagraphBlock paragraphBlock:
                    return HasMarkup(paragraphBlock.Inline);
                case DelimiterInline _:
                case EmphasisInline _:
                    return true;
                case ContainerInline containerInline:
                    var iter = containerInline.FirstChild;
                    while (iter != null)
                    {
                        if (HasMarkup(iter))
                            return true;
                        iter = iter.NextSibling;
                    }
                    return false;
                case LineBreakInline _:
                case LiteralInline _:
                    return false;
                default:
                    return true;
            }
        }

        public IEnumerable<(PartType Type, string Content)> GetParts(string content, NoteMetadata metadata)
        {
            // Here we decide whether we use markdown or not
            var doc = Markdown.Parse(content);
            var useMarkdown = false; // HasMarkup(doc); // TODO: enable markdown but fix formatting errors https://github.com/lapulpeta/Nostrid/issues/13
            if (useMarkdown)
            {
                return GetPartsInternal(Markdown.ToHtml(doc, pipeline).Replace("\n", "<br/>"), metadata, false);
            }
            else
            {
                return GetPartsInternal(content.Replace("\n", "<br/>"), metadata, true);
            }
        }

        private IEnumerable<(PartType Type, string Content)> GetPartsInternal(string content, NoteMetadata metadata, bool processLinks)
        {
            var regex = processLinks ? partsWithLinksRegex : partsRegex;
            foreach (var part in regex.Split(content))
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                var match = mentionWithIndexRegex.Match(part);
                if (match.Success)
                {
                    var index = int.Parse(match.Groups[1].Value);
                    if (metadata.EventMentions.ContainsKey(index))
                    {
                        yield return (PartType.Event, metadata.EventMentions[index]);
                    }
                    else if (metadata.AccountMentions.ContainsKey(index))
                    {
                        yield return (PartType.Account, metadata.AccountMentions[index]);
                    }
                    else
                    {
                        yield return (PartType.Text, part);
                    }
                }
                else
                {
                    match = hashtagRegex.Match(part);
                    if (match.Success)
                    {
                        yield return (PartType.Hashtag, match.Groups[1].Value);
                    }
                    else
                    {
                        match = linkRegex.Match(part);
                        if (processLinks && match.Success && Uri.IsWellFormedUriString(match.Groups[1].Value, UriKind.Absolute))
                        {
                            yield return (PartType.Link, match.Groups[1].Value);
                        }
                        else
                        {
                            yield return (PartType.Text, part);
                        }
                    }
                }
            }
        }

        [GeneratedRegex($"#({LabelRegexText})", RegexOptions.Compiled)]
        private static partial Regex HashtagRegex();

        [GeneratedRegex($"(#(?:\\[{NumberRegexText}\\]|{LabelRegexText}))", RegexOptions.Compiled)]
        private static partial Regex PartsRegex();

        [GeneratedRegex($"(#(?:\\[{NumberRegexText}\\]|{LabelRegexText})|{LinkRegexText})", RegexOptions.Compiled)]
        private static partial Regex PartsWithLinksRegex();

        [GeneratedRegex($"({LinkRegexText})", RegexOptions.Compiled)]
        private static partial Regex LinkRegex();

        [GeneratedRegex($"#\\[({NumberRegexText})\\]", RegexOptions.Compiled)]
        private static partial Regex MentionWithIndexRegex();
    }

    #region Helper Classes
    public class MyParagraphExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            renderer.ObjectRenderers.RemoveAll(x => x is ParagraphRenderer);
            renderer.ObjectRenderers.Add(new CustomParagraphRenderer());
        }
    }

    public class CustomParagraphRenderer : ParagraphRenderer
    {
        protected override void Write(HtmlRenderer renderer, ParagraphBlock obj)
        {
            if (obj.Parent is MarkdownDocument)
            {
                if (!renderer.IsFirstInContainer)
                {
                    renderer.EnsureLine();
                }
                renderer.WriteLeafInline(obj);
                if (!renderer.IsLastInContainer)
                {
                    renderer.WriteLine("<br />");
                }
                else
                {
                    renderer.EnsureLine();
                }
            }
            else
            {
                base.Write(renderer, obj);
            }
        }
    }

    public enum PartType
    {
        Text,
        Event,
        Account,
        Hashtag,
        Link
    }
    #endregion
}
