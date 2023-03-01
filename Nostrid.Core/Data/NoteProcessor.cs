using Ganss.Xss;
using HtmlAgilityPack;
using Markdig;
using Markdig.Extensions.AutoLinks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Nostrid.Misc;
using Nostrid.Model;
using Nostrid.Shared;
using System.Text.RegularExpressions;
using System.Web;

namespace Nostrid.Data
{
    public partial class NoteProcessor
    {
        private const string NumberRegexText = "\\d+";
        private const string LabelRegexText = "[a-zA-Z0-9_]+";
        private static readonly Regex partsRegex = PartsRegex();
        private static readonly Regex hashtagRegex = HashtagRegex();
        private static readonly Regex mentionWithIndexRegex = MentionWithIndexRegex();

        private readonly MarkdownPipeline pipeline;
        private readonly HtmlSanitizer htmlSanitizer;
        private readonly AccountService accountService;

        public NoteProcessor(HtmlSanitizer htmlSanitizer, AccountService accountService)
        {
            this.htmlSanitizer = htmlSanitizer;
            this.accountService = accountService;

            var builder = new MarkdownPipelineBuilder();
            builder.Extensions.Add(new AutoLinkExtension(new AutoLinkOptions() { OpenInNewWindow = true }));
            pipeline = builder.Build();
        }

        public RenderFragment GetContent(string content, List<Mention>? mentions)
        {
            var html = Markdown.Parse(content.Replace("\n", "\u0020\u0020\n"), pipeline).ToHtml(pipeline);
            
            html = htmlSanitizer.Sanitize(html);

            var document = new HtmlDocument();
            document.LoadHtml(html);

            return new RenderFragment(builder => InsertElements(0, builder, document.DocumentNode.ChildNodes, mentions));
        }

        private int InsertElements(int sequence, RenderTreeBuilder builder, HtmlNodeCollection nodes, List<Mention>? mentions)
        {
            foreach (HtmlNode node in nodes)
            {
                if (node.NodeType == HtmlNodeType.Element)
                { 
                    if (node.Name == "a")
                    {
                        var url = node.Attributes["href"]?.Value ?? string.Empty;
                        builder.OpenComponent<AsyncLink>(sequence++);
                        builder.AddAttribute(sequence++, "Url", url);
                        builder.AddAttribute(sequence++, "StopPropagation", true);
                        if (node.InnerText != url)
                        {
                            builder.AddAttribute(
                                sequence++,
                                "ChildContent",
                                new RenderFragment(b => sequence = InsertElements(sequence, b, node.ChildNodes, mentions)));
                        }
                        builder.CloseComponent();
                    }
                    else
                    { 
                        builder.OpenElement(sequence++, node.Name);
                        foreach (var attribute in node.Attributes)
                        {
                            builder.AddAttribute(sequence++, attribute.Name, attribute.Value);
                        }
                        sequence = InsertElements(sequence, builder, node.ChildNodes, mentions);
                        builder.CloseElement();
                    }
                }
                else if (node.NodeType == HtmlNodeType.Text)
                {
                    sequence = InsertElements(sequence, builder, node.InnerHtml, mentions);
                }
            }
            return sequence;
        }

        private int InsertElements(int sequence, RenderTreeBuilder builder, string content, List<Mention>? mentions)
        {
            foreach (var part in partsRegex.Split(content))
            {
                if (string.IsNullOrEmpty(part))
                { 
                    continue;
                }
                var match = mentionWithIndexRegex.Match(part);
                if (match.Success)
                {
                    var index = int.Parse(match.Groups[1].Value);
                    var value = mentions.FirstOrDefault(m => m.Type == 'e' && m.Index == index);
                    if (value != null)
                    {
                        sequence = InsertLink(
                            sequence, builder, $"/note/{value.MentionId}",
                            ByteTools.PubkeyToNote(value.MentionId, true));
                    }
                    else
                    {
                        value = mentions.FirstOrDefault(m => m.Type == 'p' && m.Index == index);
                        if (value != null)
                        {
                            sequence = InsertLink(
                                sequence, builder, $"/account/{value.MentionId}",
                                new RenderFragment(b =>
                                {
                                    b.OpenComponent<AccountName>(sequence++);
                                    b.AddAttribute(sequence++, "Id", value.MentionId);
                                    b.AddAttribute(sequence++, "OnlyText", true);
                                    b.CloseComponent();
                                }));
                        }
                        else
                        {
                            builder.AddMarkupContent(sequence++, part);
                        }
                    }
                }
                else
                {
                    match = hashtagRegex.Match(part);
                    if (match.Success)
                    {
                        var value = match.Groups[1].Value;
                        sequence = InsertLink(sequence, builder, $"/tag/{value}", $"#{value}");
                    }
                    else
                    {
                        builder.AddMarkupContent(sequence++, part);
                    }
                }
            }
            return sequence;
        }

        public int InsertLink(int sequence, RenderTreeBuilder builder, string url, string content)
        {
            builder.OpenComponent<NavLink>(sequence++);
            builder.AddAttribute(sequence++, "href", url);
            builder.AddEventStopPropagationAttribute(sequence++, "onclick", true);
            builder.AddAttribute(sequence++, "ChildContent", new RenderFragment(b => b.AddContent(sequence++, content)));
            builder.CloseComponent();
            return sequence++;
        }

        public int InsertLink(int sequence, RenderTreeBuilder builder, string url, RenderFragment content)
        {
            builder.OpenComponent<NavLink>(sequence++);
            builder.AddAttribute(sequence++, "href", url);
            builder.AddEventStopPropagationAttribute(sequence++, "onclick", true);
            builder.AddAttribute(sequence++, "ChildContent", content);
            builder.CloseComponent();
            return sequence++;
        }

        [GeneratedRegex($"(#(?:\\[{NumberRegexText}\\]|{LabelRegexText}))", RegexOptions.Compiled)]
        private static partial Regex PartsRegex();

        [GeneratedRegex($"#({LabelRegexText})", RegexOptions.Compiled)]
        private static partial Regex HashtagRegex();

        [GeneratedRegex($"#\\[({NumberRegexText})\\]", RegexOptions.Compiled)]
        private static partial Regex MentionWithIndexRegex();
    }
}
