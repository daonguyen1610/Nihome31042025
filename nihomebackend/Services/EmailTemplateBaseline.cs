using System.Net;
using System.Xml.Linq;

namespace NihomeBackend.Services;

internal static class EmailTemplateBaseline
{
    private const string Layout = """
        <div style="margin:0;padding:24px;background:#f3f6fb;font-family:Arial,sans-serif;color:#1f2937">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb">
            <tr><td style="padding:18px 24px;background:#e5394a;color:#ffffff">
              <div style="font-size:20px;font-weight:700">NICON</div>
              <div style="font-size:12px;margin:6px 0 0">{{heading}}</div>
            </td></tr>
            <tr><td style="padding:24px">
        {{content}}
            </td></tr>
            <tr><td style="padding:12px 24px;background:#f8fafc;color:#64748b;font-size:12px;line-height:18px">{{footer}}</td></tr>
          </table>
        </div>
        """;

    public static string Wrap(string heading, string content, string footer) =>
        Render(heading, content, footer);

    public static string Apply(string body, string heading, string footer)
    {
        var content = body;
        try
        {
            var root = XDocument.Parse(body).Root;
            var table = root?.Element("table");
            var rows = table?.Elements("tr").ToList();
            if (rows is { Count: 3 } &&
                string.Equals(table?.Attribute("role")?.Value, "presentation", StringComparison.OrdinalIgnoreCase))
            {
                if (rows[0].Element("td")?.Elements("div").FirstOrDefault()?.Value == "NICON")
                    heading = rows[0].Element("td")?.Elements("div").LastOrDefault()?.Value ?? heading;
                content = string.Concat(rows[1].Element("td")?.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)) ?? []);
                footer = rows[2].Element("td")?.Value ?? footer;
            }
        }
        catch (System.Xml.XmlException)
        {
            // Legacy custom HTML remains intact as the content of the common shell.
        }
        return Render(heading, content, footer);
    }

    private static string Render(string heading, string content, string footer) =>
        Layout.Replace("{{heading}}", WebUtility.HtmlEncode(heading), StringComparison.Ordinal)
            .Replace("{{content}}", content, StringComparison.Ordinal)
            .Replace("{{footer}}", WebUtility.HtmlEncode(footer), StringComparison.Ordinal);
}
