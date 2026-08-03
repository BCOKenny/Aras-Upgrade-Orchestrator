using System.Collections.ObjectModel;
using System.Xml;
using System.Xml.Linq;

namespace ArasUpgradeOrchestrator.Core.Aml;

public enum AmlNodeKind
{
    AmlRoot,
    Item,
    ScalarProperty,
    ItemProperty,
    RelationshipsContainer,
    RelationshipItem
}

public sealed class AmlParseException : Exception
{
    public AmlParseException(string message, string? sourceName, Exception? innerException = null)
        : base(sourceName is null ? message : $"{sourceName}: {message}", innerException) => SourceName = sourceName;

    public string? SourceName { get; }
}

public sealed class AmlNode
{
    private readonly XElement _element;
    private readonly IReadOnlyList<AmlNode> _children;

    internal AmlNode(XElement element, AmlNodeKind kind, AmlNode? parent)
    {
        _element = element;
        Kind = kind;
        Parent = parent;
        Name = element.Name.LocalName;
        Depth = parent is null ? 0 : parent.Depth + 1;
        Attributes = new ReadOnlyDictionary<XName, string>(
            element.Attributes().ToDictionary(attribute => attribute.Name, attribute => attribute.Value));
        ItemType = IsItem ? Attribute("type") : null;
        ItemId = IsItem ? Attribute("id") : null;
        Action = IsItem ? Attribute("action") : null;
        Where = IsItem ? Attribute("where") : null;
        Path = BuildPath();
        _children = element.Elements().Select(child => new AmlNode(child, Classify(child, kind), this)).ToArray();
    }

    public AmlNodeKind Kind { get; }
    public string Name { get; }
    public XName QualifiedName => _element.Name;
    public int Depth { get; }
    public AmlNode? Parent { get; }
    public IReadOnlyDictionary<XName, string> Attributes { get; }
    public IReadOnlyList<AmlNode> Children => _children;
    public string Path { get; }
    public string? ItemType { get; }
    public string? ItemId { get; }
    public string? Action { get; }
    public string? Where { get; }
    public string? ScalarValue => Kind == AmlNodeKind.ScalarProperty ? _element.Value : null;
    public bool IsItem => Kind is AmlNodeKind.Item or AmlNodeKind.RelationshipItem;

    public IEnumerable<AmlNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in _children)
        foreach (var descendant in child.DescendantsAndSelf())
            yield return descendant;
    }

    public XElement CloneSubtree() => new(_element);

    private string? Attribute(string localName) =>
        _element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private string BuildPath()
    {
        if (Kind == AmlNodeKind.AmlRoot) return "/AML";
        var prefix = Parent?.Path ?? string.Empty;
        return Kind switch
        {
            AmlNodeKind.Item or AmlNodeKind.RelationshipItem => $"{prefix}/Item[{ItemDescriptor()}]",
            AmlNodeKind.ScalarProperty => $"{prefix}/ScalarProperty[name={Name}]",
            AmlNodeKind.ItemProperty => $"{prefix}/ItemProperty[name={Name}]",
            AmlNodeKind.RelationshipsContainer => $"{prefix}/Relationships",
            _ => throw new InvalidOperationException($"不支援的 AML 節點類型 {Kind}。")
        };
    }

    private string ItemDescriptor()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(ItemType)) parts.Add($"type={ItemType}");
        if (!string.IsNullOrWhiteSpace(ItemId)) parts.Add($"id={ItemId}");
        else
        {
            var name = _element.Elements().FirstOrDefault(element =>
                element.Name.LocalName == "name" && !element.Elements().Any(child => child.Name.LocalName == "Item"))?.Value;
            if (!string.IsNullOrWhiteSpace(name)) parts.Add($"name={name.Trim()}");
        }
        return string.Join(", ", parts);
    }

    private static AmlNodeKind Classify(XElement element, AmlNodeKind parentKind)
    {
        if (element.Name.LocalName == "AML") return AmlNodeKind.AmlRoot;
        if (element.Name.LocalName == "Item")
            return parentKind == AmlNodeKind.RelationshipsContainer ? AmlNodeKind.RelationshipItem : AmlNodeKind.Item;
        if (element.Name.LocalName == "Relationships") return AmlNodeKind.RelationshipsContainer;
        return element.Elements().Any(child => child.Name.LocalName == "Item")
            ? AmlNodeKind.ItemProperty
            : AmlNodeKind.ScalarProperty;
    }
}

public sealed class AmlDocument
{
    private readonly XDocument _document;

    private AmlDocument(XDocument document, string? sourceName)
    {
        if (document.Root is null || document.Root.Name.LocalName != "AML")
            throw new AmlParseException("根節點必須是 AML。", sourceName);
        _document = document;
        SourceName = sourceName;
        Root = new AmlNode(document.Root, AmlNodeKind.AmlRoot, null);
        TopLevelItems = Root.Children.Where(node => node.Kind == AmlNodeKind.Item).ToArray();
    }

    public string? SourceName { get; }
    public AmlNode Root { get; }
    public IReadOnlyList<AmlNode> TopLevelItems { get; }
    public XDeclaration? Declaration => _document.Declaration is null ? null : new XDeclaration(_document.Declaration);

    public static AmlDocument Parse(string xml, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(xml);
        using var textReader = new StringReader(xml);
        return Read(textReader, sourceName);
    }

    public static AmlDocument Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("AML 檔案路徑不可為空。", nameof(path));
        var fullPath = Path.GetFullPath(path);
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            return Read(reader, fullPath);
        }
        catch (AmlParseException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AmlParseException("AML 檔案無法安全讀取。", fullPath, exception);
        }
    }

    public string ToXml(SaveOptions options = SaveOptions.DisableFormatting) =>
        _document.Declaration is null
            ? _document.ToString(options)
            : $"{_document.Declaration}{Environment.NewLine}{_document.ToString(options)}";

    private static AmlDocument Read(TextReader input, string? sourceName)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = false,
            IgnoreComments = false,
            CloseInput = false
        };
        try
        {
            using var reader = XmlReader.Create(input, settings);
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            return new AmlDocument(document, sourceName);
        }
        catch (AmlParseException) { throw; }
        catch (XmlException exception)
        {
            throw new AmlParseException($"AML XML 無法解析（line {exception.LineNumber}, position {exception.LinePosition}）。", sourceName, exception);
        }
    }
}
