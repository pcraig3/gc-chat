using AIChatApp.Models;
using System.Text;

namespace AIChatApp.Services;

public class PromptBuilderService
{
  public string Build(string userQuery, List<Source> docs)
  {
    var prompt = new StringBuilder();

    if (docs.Count > 0)
    {
      // Build a context string from returned document snippets
      // each "Document" includes its title to preserve the context of what the document is about
      var context = string.Join("\n\n---\n\n", docs.Select((d, i) =>
          $"DOCUMENT {i + 1}\nTITLE: {d.Title}\nCONTENT: {d.Chunk}"));

      // This is the main prompt we are using.
      prompt.AppendLine(GetSystemPrompt());
      prompt.AppendLine("You are helping people answer questions based on the context below, denoted by the <context> tag. Past questions and answers in this conversation may also provide required context.");
      prompt.AppendLine("Always cite sources at the end of each relevant sentence using this exact format: [Exact Document Title.ext]. If multiple sources support a sentence, list each in its own bracket token separated by a comma and space (eg, [Doc A.pdf], [Doc B.docx]). Even if you mention a document title in a sentence, still append a bracketed citation at the end of that sentence (eg, These steps are summarized in **Document.docx** [Document.docx].).");
      prompt.AppendLine("IMPORTANT: Put exactly one document title per bracket. Do not combine multiple titles in a single bracket. Do not prefix with 'Source:' or 'Sources:'. If multiple sources apply to a sentence, chain separate brackets back-to-back like [Title A.docx] [Title-B.pdf]. Never use numeric citations like [1]. Always use the EXACT source document title.");
      prompt.AppendLine("Answer in 2-3 sentences unless otherwise instructed.");
      prompt.AppendLine("VERY IMPORTANT: If you don't know the answer, just say \"I don't know.\", don't try to make up an answer.");
      prompt.AppendLine();
      prompt.AppendLine("<context>");
      prompt.AppendLine(context);
      prompt.AppendLine("</context>");
    }
    else
    {
      // This "backup" prompt is used if we don't have source documents.
      // This is here as a backup in case we are not connected to the search service and can't retrieve sources.
      prompt.AppendLine(GetSystemPrompt());
    }

    prompt.AppendLine();
    prompt.AppendLine("CRITICAL: Detect the language of the user's question below. If there is no explict direction on which language to use, you should respond in THE EXACT SAME LANGUAGE. If the question tells you which language to respond in, use that language.");
    prompt.AppendLine("<question>");
    prompt.AppendLine(userQuery);
    prompt.AppendLine("</question>");
    return prompt.ToString();
  }

  public static string GetSystemPrompt() =>
    $"You are a helpful AI assistant for information about Canada and the Canadian government. Your name is GC Chat. It is {DateTime.Now.ToString("yyyy")} right now. Respond to users in their preferred language.";
}
