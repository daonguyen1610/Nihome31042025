using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

// Writes values into a template the way Excel stores a result, keeping each cell's formula and style.
// ClosedXML would drop every stored result on save, and recalculating the large bases takes it minutes.
internal static class BoqWorkbookPatch
{
    public static byte[] Apply(BoqTemplate template, Dictionary<(string Sheet, string Cell), double> values)
    {
        using var output = new MemoryStream();
        output.Write(ProjectBoqTemplateCatalog.ReadFile(template));
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
        {
            foreach (var sheet in values.GroupBy(value => value.Key.Sheet))
            {
                var entry = zip.GetEntry(SheetPart(zip, sheet.Key))!;
                string xml;
                using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
                foreach (var ((_, cell), value) in sheet)
                {
                    var match = Regex.Match(xml, $@"<c r=""{cell}""([^>]*?)(/>|>(.*?)</c>)", RegexOptions.Singleline);
                    Assert.True(match.Success, $"{sheet.Key}!{cell}");
                    var attributes = Regex.Replace(match.Groups[1].Value, @"\s*t=""[^""]*""", "");
                    var formula = Regex.Match(match.Groups[3].Value, "<f.*?(?:</f>|/>)", RegexOptions.Singleline).Value;
                    xml = xml.Replace(match.Value, $@"<c r=""{cell}""{attributes}>{formula}<v>{value.ToString(CultureInfo.InvariantCulture)}</v></c>");
                }
                entry.Delete();
                using var writer = new StreamWriter(zip.CreateEntry(entry.FullName).Open());
                writer.Write(xml);
            }
        }
        return output.ToArray();
    }

    private static string SheetPart(ZipArchive zip, string sheet)
    {
        string Read(string name) { using var reader = new StreamReader(zip.GetEntry(name)!.Open()); return reader.ReadToEnd(); }
        var id = Regex.Match(Read("xl/workbook.xml"), $@"<sheet name=""{Regex.Escape(sheet.Replace("&", "&amp;"))}""[^>]*r:id=""([^""]+)""").Groups[1].Value;
        return "xl/" + Regex.Match(Read("xl/_rels/workbook.xml.rels"), $@"Id=""{id}""[^>]*Target=""([^""]+)""").Groups[1].Value;
    }
}
