using AIChatApp.Models;
using System.Text.RegularExpressions;

namespace AIChatApp.Services;

public class CitationHelperService
{
  // Extract document titles formatted like "[Document.txt]" from answer text
  public List<string> ExtractCitedDocuments(string answer)
  {
    var citedDocs = new List<string>();
    var regex = new Regex(@"\[(.*?)\]");
    var matches = regex.Matches(answer);

    foreach (Match match in matches)
    {
      if (match.Success && match.Groups.Count > 1)
      {
        string docTitle = match.Groups[1].Value;
        citedDocs.Add(docTitle);
      }
    }

    return citedDocs;
  }

  // Filter full document list down to only the ones actually cited
  public List<Source> FilterSourcesByCitedDocuments(List<Source> allSources, List<string> citedDocs)
  {
    var filteredSources = new List<Source>();

    foreach (var docTitle in citedDocs)
    {
      var source = allSources.FirstOrDefault(s =>
          string.Equals(s.Title?.Trim(), docTitle?.Trim(), StringComparison.OrdinalIgnoreCase) &&
          !filteredSources.Any(fs =>
              string.Equals(fs.Title?.Trim(), docTitle?.Trim(), StringComparison.OrdinalIgnoreCase)));

      if (source != null)
        filteredSources.Add(source);
    }

    return filteredSources;
  }

  // Replace "[Document Title]" with superscript numbers like "<sup>[2]</sup>"
  public string ReplaceTitleCitationsWithNumberCitations(string answer, List<string> citedDocs)
  {
    var uniqueCitedDocs = new List<string>();
    var docToNumberMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < citedDocs.Count; i++)
    {
      var docTitle = citedDocs[i].Trim();
      if (!docToNumberMap.ContainsKey(docTitle))
      {
        docToNumberMap[docTitle] = uniqueCitedDocs.Count + 1;
        uniqueCitedDocs.Add(docTitle);
      }
    }

    var regex = new Regex(@"\[(.*?)\]");
    var result = regex.Replace(answer, match =>
    {
      string docTitle = match.Groups[1].Value.Trim();
      return docToNumberMap.TryGetValue(docTitle, out int number)
              ? $"<sup>[{number}]</sup>"
              : match.Value;
    });

    return result;
  }
}
