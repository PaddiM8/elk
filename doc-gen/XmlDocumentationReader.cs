using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Elk.DocGen;

class FunctionDocumentation
{
    public string Name { get; }

    public List<(string descrption, string? types)> Parameters { get; } = new();

    public string? Returns { get; set; }

    public string? Example { get; set; }

    public string? Summary { get; set; }

    public FunctionDocumentation(string name)
    {
        Name = name;
    }
}

class XmlDocumentationReader : IDisposable
{
    private readonly XmlReader _reader;

    public XmlDocumentationReader(string xmlPath)
    {
        _reader = XmlReader.Create(xmlPath);
    }

    public void Dispose()
    {
        _reader.Dispose();
    }

    public Dictionary<string, FunctionDocumentation> Read()
    {
        var docs = new Dictionary<string, FunctionDocumentation>();
        FunctionDocumentation? currentFunctionDocs = null;
        while (_reader.Read())
        {
            if (_reader.Depth <= 2 && currentFunctionDocs != null)
            {
                docs.Add(currentFunctionDocs.Name, currentFunctionDocs);
                currentFunctionDocs = null;
            }

            if (MatchElement("member") && _reader["name"]?.StartsWith("M:Elk.Std") is true)
            {
                string fullName = _reader["name"]!;
                int parenthesisIndex = fullName.IndexOf('(');
                string name = parenthesisIndex == -1
                    ? fullName[2..]
                    : fullName[2..fullName.IndexOf('(')];
                currentFunctionDocs = new FunctionDocumentation(name);
            }

            if (MatchElement("param") && currentFunctionDocs != null)
            {
                string? types = _reader["types"];
                currentFunctionDocs.Parameters.Add((_reader.ReadInnerXml(), types));
            }

            if (MatchElement("returns") && currentFunctionDocs != null)
            {
                currentFunctionDocs.Returns = RemoveIndentation(_reader.ReadInnerXml());
            }

            if (MatchElement("example") && currentFunctionDocs != null)
            {
                currentFunctionDocs.Example = RemoveIndentation(_reader.ReadInnerXml());
            }

            if (MatchElement("summary") && currentFunctionDocs != null)
            {
                currentFunctionDocs.Summary = RemoveIndentation(_reader.ReadInnerXml());
            }
        }

        return docs;
    }

    private bool MatchElement(string name)
        => _reader.NodeType == XmlNodeType.Element && _reader.Name == name;

    private string RemoveIndentation(string value)
        => string.Join('\n', value.Split('\n').Select(x => x.Trim())).Trim();
}