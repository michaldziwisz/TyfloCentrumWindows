using Tyflocentrum.Windows.Domain.Models;
using Tyflocentrum.Windows.Domain.Text;
using Tyflocentrum.Windows.UI.Formatting;

namespace Tyflocentrum.Windows.UI.ViewModels;

public sealed class TyfloSwiatMagazineIssueItemViewModel
{
    public TyfloSwiatMagazineIssueItemViewModel(WpPostSummary item)
    {
        IssueId = item.Id;
        Title = WordPressTextFormatter.NormalizeHtml(item.Title.Rendered);
        Link = item.Link;
        PublishedDate = WordPressTextFormatter.FormatDate(item.Date);

        var parsed = TyfloSwiatMagazineParsing.ParseIssueNumberAndYear(Title);
        IssueNumber = parsed.Number;
        Year = parsed.Year ?? 0;
    }

    public int IssueId { get; }

    public string Title { get; }

    public string Link { get; }

    public string PublishedDate { get; }

    public int? IssueNumber { get; }

    public int Year { get; }

    public string NumberLabel => IssueNumber is int number ? $"Numer {number}" : "Numer czasopisma";

    public string OpenLinkLabel => $"Otwórz numer w przeglądarce: {Title}";

    public string AccessibleLabel
    {
        get
        {
            var parts = new List<string> { NumberLabel, Title };

            if (!string.IsNullOrWhiteSpace(PublishedDate))
            {
                parts.Add(PublishedDate);
            }

            return string.Join(". ", parts);
        }
    }

    public override string ToString() => AccessibleLabel;
}
