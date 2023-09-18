using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Elk.Std.DataTypes.Serialization;

public class CsvParser : IEnumerable<RuntimeObject>
{
    private readonly IEnumerable<string> _lines;
    private readonly char _separator;
    private readonly StringBuilder _unfinishedLine = new();

    public CsvParser(IEnumerable<string> lines, char separator)
    {
        _lines = lines;
        _separator = separator;
    }

    public IEnumerator<RuntimeObject> GetEnumerator()
        => _lines
            .Select(ParseLine)
            .Where(x => x != null)
            .GetEnumerator()!;

    private RuntimeList? ParseLine(string line)
    {
        if (line.Length == 0)
            return null;

        int quoteCount = line.Count(x => x == '"');
        if (quoteCount % 2 != 0)
        {
            _unfinishedLine.AppendLine(line);

            return null;
        }

        string row = line;
        if (_unfinishedLine.Length > 0)
        {
            _unfinishedLine.Append(line);
            row = _unfinishedLine.ToString();
            _unfinishedLine.Clear();
        }

        var columns = new List<RuntimeString>();
        bool inQuotes = false;
        int lastSeparatorPos = -1;
        for (int i = 0; i < row.Length; i++)
        {
            char c = row[i];
            if (c == '"')
                inQuotes = !inQuotes;

            if (inQuotes)
                continue;

            if (c == _separator)
            {
                int start = lastSeparatorPos + 1;
                columns.Add(new RuntimeString(row[start..i]));
                lastSeparatorPos = i;
            }
        }

        // The final column
        int finalSeparatorEnd = lastSeparatorPos + 1;
        if (row.Length > finalSeparatorEnd)
            columns.Add(new RuntimeString(row[finalSeparatorEnd..row.Length]));

        return new RuntimeList(columns);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}