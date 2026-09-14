using System.Xml;
using System.Xml.Serialization;

namespace Reaparr.PublicAPI;

[XmlRoot("rss")]
public record TorznabMediaSearchResponseDTO
{
    [XmlAttribute("version")]
    public string Version { get; set; } = "2.0";

    [XmlElement("channel")]
    public TorznabChannel Channel { get; set; } = new();

    // Declare namespaces so XmlSerializer knows about "torznab"
    [XmlNamespaceDeclarations]
    public XmlSerializerNamespaces Xmlns { get; set; } =
        new([
            new XmlQualifiedName("torznab", "http://torznab.com/schemas/2015/feed"),
            new XmlQualifiedName("newznab", "http://www.newznab.com/DTD/2010/feeds/attributes/"),
        ]);
}

public record TorznabChannel
{
    [XmlElement("title")]
    public string Title { get; set; } = "PlexRipper Torznab";

    [XmlElement("description")]
    public string Description { get; set; } = "TV search results";

    [XmlElement("language")]
    public string Language { get; set; } = "en-us";

    [XmlElement("category")]
    public string Category { get; set; } = "search";

    [XmlElement("item")]
    public List<TorznabItem> Items { get; set; } = new();

    [XmlElement("response", Namespace = "http://www.newznab.com/DTD/2010/feeds/attributes/")]
    public TorznabResponseMetadata Response { get; set; } = new();
}

public record TorznabResponseMetadata
{
    [XmlAttribute("offset")]
    public int Offset { get; set; }

    [XmlAttribute("total")]
    public int Total { get; set; }
}

public record TorznabItem
{
    [XmlElement("title")]
    public string Title { get; set; } = string.Empty;

    [XmlElement("guid")]
    public TorznabGuid Guid { get; set; } = new();

    [XmlElement("link")]
    public string Link { get; set; } = string.Empty;

    [XmlElement("pubDate")]
    public string PubDate { get; set; } = DateTime.UtcNow.ToString("R");

    [XmlElement("size")]
    public long Size { get; set; }

    // Instead of torznab:attr, map to namespace
    [XmlElement("attr", Namespace = "http://torznab.com/schemas/2015/feed")]
    public List<TorznabAttr> Attributes { get; set; } = new();

    [XmlElement("enclosure")]
    public TorznabEnclosure Enclosure { get; set; } = new();

    [XmlIgnore]
    public DateTime SortAddedAt { get; set; }

    [XmlIgnore]
    public string SortMachineIdentifier { get; set; } = string.Empty;

    [XmlIgnore]
    public PlexMediaType SortMediaType { get; set; }

    [XmlIgnore]
    public int SortRatingKey { get; set; }

    [XmlIgnore]
    public int SortMediaId { get; set; }

    [XmlIgnore]
    public int SortPartId { get; set; }
}

public record TorznabGuid
{
    [XmlAttribute("isPermaLink")]
    public string IsPermaLink { get; set; } = "false";

    [XmlText]
    public string Value { get; set; } = string.Empty;
}

public record TorznabAttr
{
    public TorznabAttr() { }

    public TorznabAttr(string name, string value)
    {
        Name = name;
        Value = value;
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("value")]
    public string Value { get; set; } = string.Empty;
}

public record TorznabEnclosure
{
    [XmlAttribute("url")]
    public string Url { get; set; } = string.Empty;

    [XmlAttribute("length")]
    public long Length { get; set; }

    [XmlAttribute("type")]
    public string Type { get; set; } = "application/x-bittorrent";
}

[XmlRoot("error")]
public record TorznabErrorResponseDTO
{
    [XmlAttribute("code")]
    public int Code { get; set; }

    [XmlAttribute("description")]
    public string Description { get; set; } = string.Empty;
}
