using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LagoVista.AI.Rag.Chunkers.Models;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    public static class DdrDescriptionBuilder
    {
        // Matches things like:
        // "IDX-001 Title" or "IDX-001 – Title" or just "IDX-001"
        private static readonly Regex DdrIdRegex =
            new Regex(@"^(?<type>[A-Za-z]+)-(?<num>\d+)\s*(?<title>.*)$", RegexOptions.Compiled);

        /// <summary>
        /// Builds a DdrDescription from DDR markdown text.
        /// </summary>
        public static InvokeResult<DdrDescription> FromSource(IndexFileContext ctx, string sourceText)
        {
            var result = new InvokeResult<DdrDescription>();

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                result.AddUserError("Source text is empty.");
                return result;
            }

            // Normalize line endings and split into lines so we can inspect header + meta rows.
            var normalized = sourceText.Replace("\r\n", "\n");
            var lines = normalized.Split('\n');

            var firstNonEmpty = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
            if (string.IsNullOrWhiteSpace(firstNonEmpty) || !firstNonEmpty.TrimStart().StartsWith("#"))
            {
                result.AddUserError("DDR must begin with a markdown heading containing the DDR id.");
                return result;
            }

            var firstLine = firstNonEmpty.TrimEnd('\r');
            var headingText = firstLine.TrimStart().TrimStart('#').Trim();

            var match = DdrIdRegex.Match(headingText);
            if (!match.Success)
            {
                result.AddUserError($"Unable to parse DDR identifier from first line: \"{headingText}\"");
                return result;
            }

            var ddrType = match.Groups["type"].Value;
            var numText = match.Groups["num"].Value;
            var headingTitle = match.Groups["title"].Value?.Trim();

            if (string.IsNullOrWhiteSpace(ddrType))
            {
                result.AddUserError("DDR type could not be parsed from heading.");
                return result;
            }

            if (!int.TryParse(numText, out var ddrNumber))
            {
                result.AddUserError($"Unable to parse DDR number: \"{numText}\"");
                return result;
            }

            // Clean up heading title (strip leading punctuation like "–" or "-")
            if (!string.IsNullOrWhiteSpace(headingTitle))
            {
                headingTitle = headingTitle.TrimStart(' ', '-', '–', '—', ':').Trim();
            }

            // If we don't have a title in the heading, fall back to a "**Title:**" meta line.
            var metaTitle = string.IsNullOrWhiteSpace(headingTitle)
                ? TryExtractMetaValue(lines, "Title")
                : null;

            var finalTitle = !string.IsNullOrWhiteSpace(headingTitle)
                ? headingTitle
                : metaTitle;

            if (string.IsNullOrWhiteSpace(finalTitle))
            {
                result.AddUserError("DDR title could not be extracted from heading or Title metadata.");
                return result;
            }

            // Optional: **ModeStatus:** line
            var status = TryExtractMetaValue(lines, "Status");

            var description = new DdrDescription
            {
                DdrType = ddrType,
                DdrNumber = ddrNumber,
                DdrTitle = finalTitle,
                HeaderBlock = firstLine.Trim()
            };

            description.SetCommonProperties(ctx);
           
            // If you've added this to the model, wire it up:
            if (!string.IsNullOrWhiteSpace(status))
            {
                description.Status = status; // you said you'll add this property
            }

            // Parse sections based on "##" headings (same behavior as before).
            BuildSections(sourceText, description);

            result.Result = description;
            return result;
        }

        private static string TryExtractMetaValue(string[] lines, string label)
        {
            // Looks for lines like:  "**Title:** DocId Generation Strategy"
            // or                    "**ModeStatus:** Accepted"
            var pattern = $"^\\s*\\*\\*{label}:\\*\\*\\s*(?<value>.+)\\s*$";
            var regex = new Regex(pattern);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var match = regex.Match(line.TrimEnd('\r'));
                if (match.Success)
                {
                    var value = match.Groups["value"].Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }

            return null;
        }

        private static void BuildSections(string sourceText, DdrDescription description)
        {
            // Split document into blocks beginning at "## " headings
            var blocks = Regex
                .Split(sourceText ?? string.Empty, "(?=^##\\s+)", RegexOptions.Multiline)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToList();

            // If there are no explicit sections, treat the entire document (minus first line)
            // as a single "Introduction" section.
            if (blocks.Count == 0)
            {
                var remainder = StripFirstLine(sourceText);
                description.Sections.Add(new DdrSectionDescription
                {
                    SectionKey = "introduction",
                    Heading = "Introduction",
                    RawMarkdown = remainder
                });
                return;
            }

            foreach (var block in blocks)
            {
                var trimmed = block.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                string heading = ExtractHeading(trimmed, out var key);

                description.Sections.Add(new DdrSectionDescription
                {
                    SectionKey = key,
                    Heading = heading,
                    RawMarkdown = trimmed
                });
            }
        }

        private static string StripFirstLine(string text)
        {
            using (var reader = new StringReader(text))
            {
                reader.ReadLine(); // discard first line
                return reader.ReadToEnd()?.Trim() ?? string.Empty;
            }
        }

        private static string ExtractHeading(string block, out string slug)
        {
            using (var reader = new StringReader(block))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("## "))
                    {
                        var heading = line.Substring(3).Trim();
                        slug = Slug(heading);
                        return heading;
                    }
                }
            }

            // fallback for blocks without a "## " line (e.g., header/preamble block)
            slug = "section";
            return "Section";
        }

        private static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "section";

            value = value.Trim().ToLowerInvariant();

            var chars = new List<char>(value.Length);
            var lastDash = false;

            foreach (var ch in value)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    chars.Add(ch);
                    lastDash = false;
                }
                else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_' || ch == '/')
                {
                    if (!lastDash)
                    {
                        chars.Add('-');
                        lastDash = true;
                    }
                }
            }

            var result = new string(chars.ToArray()).Trim('-');
            return string.IsNullOrWhiteSpace(result) ? "section" : result;
        }
    }
}
