using System.Collections.Generic;
using System.Text;

namespace LagoVista.AI.Services
{
    public static class PromptBuilder
    {
        public static ChatPrompt Build(string question, List<Snippet> snippets, int maxContextTokens = 6000)
        {
            var sb = new StringBuilder();
            int budget = 0;

            foreach (var s in snippets)
            {
                var est = TokenEstimator.EstimateTokens(s.Text);
                if (budget + est > maxContextTokens) break;
                budget += est;

                sb.AppendLine($"[{s.Tag}] {s.Path}:{s.Start}-{s.End}");
                sb.AppendLine("```");
                sb.AppendLine(s.Text.TrimEnd());
                sb.AppendLine("```");
                sb.AppendLine();
            }

            var system =
@"You are a senior software engineer assistant.
Use only the provided context when applicable.
If the answer is not in the context, say so.
Always cite sources using [S#] tags.  
Format the output as html that can be included
in another web page, only include the content 
within the body tag, do not include the HTML or header.
The color of the text should look good on a dark background 
and the syntax of the code should be highlighted appropriately
for the language";

            var user = question;
            var context = "Context snippets (cite with [S#]):\n\n" + sb.ToString();

            return new ChatPrompt(system, user, context);
        }
    }
}
