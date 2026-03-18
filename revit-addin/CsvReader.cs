using System.Text;

namespace BimDown.RevitAddin;

public static class CsvReader
{
    public static (IReadOnlyList<string> Columns, List<Dictionary<string, string?>> Rows) Read(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8);

        var headerLine = reader.ReadLine() ?? throw new InvalidOperationException("CSV file is empty");
        var columns = ParseLine(headerLine);

        var rows = new List<Dictionary<string, string?>>();
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = ParseLine(line);
            var row = new Dictionary<string, string?>();
            for (var i = 0; i < columns.Count; i++)
            {
                var val = i < values.Count ? values[i] : "";
                row[columns[i]] = string.IsNullOrEmpty(val) ? null : val;
            }
            rows.Add(row);
        }

        return (columns, rows);
    }

    static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var i = 0;
        while (i <= line.Length)
        {
            if (i == line.Length)
            {
                fields.Add("");
                break;
            }

            if (line[i] == '"')
            {
                // Quoted field
                var sb = new StringBuilder();
                i++; // skip opening quote
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                        }
                        else
                        {
                            i++; // skip closing quote
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(line[i]);
                        i++;
                    }
                }
                fields.Add(sb.ToString());
                if (i < line.Length && line[i] == ',') i++; // skip comma
            }
            else
            {
                // Unquoted field
                var commaIdx = line.IndexOf(',', i);
                if (commaIdx < 0)
                {
                    fields.Add(line[i..]);
                    break;
                }
                fields.Add(line[i..commaIdx]);
                i = commaIdx + 1;
            }
        }
        return fields;
    }
}
