using System.Text;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IUtf8CsvParser
{
    Task<CsvImportResult> ParseAsync(
        Stream stream,
        IReadOnlyList<string> expectedHeaders,
        int maxBytes = 2 * 1024 * 1024,
        int maxRows = 2000,
        CancellationToken ct = default);
}

public sealed class Utf8CsvParser : IUtf8CsvParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<CsvImportResult> ParseAsync(
        Stream stream,
        IReadOnlyList<string> expectedHeaders,
        int maxBytes = 2 * 1024 * 1024,
        int maxRows = 2000,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(expectedHeaders);

        if (maxBytes <= 0 || maxRows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        var result = new CsvImportResult();
        byte[] bytes;
        try
        {
            bytes = await ReadLimitedAsync(stream, maxBytes, ct);
        }
        catch (CsvLimitException)
        {
            result.Errors.Add(Error("csv.error.maxBytes", $"Tệp CSV vượt quá dung lượng tối đa {maxBytes / 1024 / 1024} MB.",
                args: new() { ["max"] = maxBytes / 1024 / 1024 }));
            return result;
        }

        string content;
        try
        {
            content = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            result.Errors.Add(Error("csv.error.utf8", "Tệp CSV phải được mã hóa UTF-8 hợp lệ."));
            return result;
        }

        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content[1..];
        }

        var parsedRows = ParseRows(content, result.Errors);
        if (result.Errors.Count > 0) return result;
        if (parsedRows.Count == 0)
        {
            result.Errors.Add(Error("csv.error.headerRequired", "Tệp CSV phải có dòng tiêu đề.", row: 1));
            return result;
        }

        var headers = parsedRows[0];
        result.Headers.AddRange(headers);
        if (!headers.SequenceEqual(expectedHeaders, StringComparer.Ordinal))
        {
            result.Errors.Add(Error("csv.error.invalidHeaders", $"Tiêu đề CSV không hợp lệ. Thứ tự bắt buộc: {string.Join(',', expectedHeaders)}.",
                row: 1, args: new() { ["headers"] = string.Join(',', expectedHeaders) }));
            return result;
        }

        if (headers.Distinct(StringComparer.Ordinal).Count() != headers.Count)
        {
            result.Errors.Add(Error("csv.error.duplicateHeaders", "Tiêu đề CSV không được trùng lặp.", row: 1));
            return result;
        }

        var dataRows = parsedRows.Skip(1).ToList();
        if (dataRows.Count > maxRows)
        {
            result.Errors.Add(Error("csv.error.maxRows", $"Tệp CSV chỉ được chứa tối đa {maxRows} dòng dữ liệu.",
                row: maxRows + 2, args: new() { ["max"] = maxRows }));
            return result;
        }

        for (var rowIndex = 0; rowIndex < dataRows.Count; rowIndex++)
        {
            var values = dataRows[rowIndex];
            if (values.Count != headers.Count)
            {
                result.Errors.Add(Error("csv.error.columnCount", $"Dòng {rowIndex + 2} phải có đúng {headers.Count} cột.",
                    row: rowIndex + 2, args: new() { ["count"] = headers.Count }));
                continue;
            }

            result.Rows.Add(headers
                .Select((header, columnIndex) => new { header, value = values[columnIndex] })
                .ToDictionary(item => item.header, item => item.value, StringComparer.Ordinal));
        }

        return result;
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream stream, int maxBytes, CancellationToken ct)
    {
        using var buffer = new MemoryStream(Math.Min(maxBytes, 64 * 1024));
        var chunk = new byte[81920];
        var total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, maxBytes + 1 - total)), ct);
            if (read == 0) break;
            total += read;
            if (total > maxBytes) throw new CsvLimitException();
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        return buffer.ToArray();
    }

    private static List<List<string>> ParseRows(string content, List<CsvImportError> errors)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var quoteClosed = false;
        var rowNumber = 1;
        var columnNumber = 1;

        for (var index = 0; index < content.Length; index++)
        {
            var current = content[index];
            if (inQuotes)
            {
                if (current != '"')
                {
                    field.Append(current);
                    continue;
                }

                if (index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = false;
                    quoteClosed = true;
                }
                continue;
            }

            if (quoteClosed && current is not ',' and not '\r' and not '\n')
            {
                errors.Add(Error("csv.error.afterClosingQuote", "Không được có ký tự sau dấu ngoặc kép đóng của trường CSV.", rowNumber, columnNumber));
                return rows;
            }

            if (current == '"')
            {
                if (field.Length != 0)
                {
                    errors.Add(Error("csv.error.quotePosition", "Dấu ngoặc kép chỉ được xuất hiện ở đầu trường CSV.", rowNumber, columnNumber));
                    return rows;
                }
                inQuotes = true;
                continue;
            }

            if (current == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                quoteClosed = false;
                columnNumber++;
                continue;
            }

            if (current is '\r' or '\n')
            {
                if (current == '\r')
                {
                    if (index + 1 >= content.Length || content[index + 1] != '\n')
                    {
                        errors.Add(Error("csv.error.lineEnding", "CSV chỉ hỗ trợ xuống dòng LF hoặc CRLF.", rowNumber));
                        return rows;
                    }
                    index++;
                }
                row.Add(field.ToString());
                rows.Add(row);
                row = [];
                field.Clear();
                quoteClosed = false;
                rowNumber++;
                columnNumber = 1;
                continue;
            }

            field.Append(current);
        }

        if (inQuotes)
        {
            errors.Add(Error("csv.error.unclosedQuote", "Trường CSV có dấu ngoặc kép chưa đóng.", rowNumber, columnNumber));
            return rows;
        }

        if (field.Length > 0 || row.Count > 0 || quoteClosed)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static CsvImportError Error(
        string messageKey,
        string message,
        int? row = null,
        int? column = null,
        Dictionary<string, object>? args = null) => new()
        {
            Row = row,
            Column = column,
            Message = message,
            MessageKey = messageKey,
            MessageArgs = args,
        };

    private sealed class CsvLimitException : Exception
    {
    }
}
