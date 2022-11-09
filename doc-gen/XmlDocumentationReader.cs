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

    public List<string> Errors { get; } = new();

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

            if (_reader.NodeType != XmlNodeType.Element)
                continue;

            var currentElement = _reader.Name;
            if (currentElement == "member" && _reader["name"]?.StartsWith("M:Elk.Std") is true)
            {
                string fullName = _reader["name"]!;
                int parenthesisIndex = fullName.IndexOf('(');
                string name = parenthesisIndex == -1
                    ? fullName[2..]
                    : fullName[2..parenthesisIndex];
                currentFunctionDocs = new FunctionDocumentation(name);
            }

            if (currentFunctionDocs == null)
                continue;

            if (currentElement == "param")
                currentFunctionDocs.Parameters.Add((_reader.ReadInnerXml(), _reader["types"]));
            else if (currentElement == "returns")
                currentFunctionDocs.Returns = ReadWithoutIndentation();
            else if (currentElement == "example")
                currentFunctionDocs.Example = ReadWithoutIndentation();
            else if (currentElement == "summary")
                currentFunctionDocs.Summary = ReadWithoutIndentation();
            else if (currentElement == "throws")
                currentFunctionDocs.Errors.Add(ReadWithoutIndentation());
        }

        return docs;
    }

    private string ReadWithoutIndentation()
        => RemoveIndentation(_reader.ReadInnerXml());

    private static string RemoveIndentation(string value)
        => string.Join('\n', value.Split('\n').Select(x => x.Trim())).Trim();
}