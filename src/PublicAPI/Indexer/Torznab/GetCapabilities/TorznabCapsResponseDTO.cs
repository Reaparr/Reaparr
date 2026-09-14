using System.Xml.Serialization;

namespace Reaparr.PublicAPI;

[XmlRoot("caps")]
public record TorznabCapsResponseDTO
{
    [XmlNamespaceDeclarations]
    public XmlSerializerNamespaces? Xmlns { get; set; }

    [XmlElement("server")]
    public TorznabServer Server { get; set; } = new();

    [XmlElement("limits")]
    public TorznabLimits Limits { get; set; } = new();

    [XmlElement("searching")]
    public TorznabSearching? Searching { get; set; } = new();

    [XmlArray("categories")]
    [XmlArrayItem("category")]
    public List<TorznabCategory> Categories { get; set; } = new();

}

public record TorznabServer
{
    [XmlAttribute("version")]
    public string Version { get; set; } = "1.0";

    [XmlAttribute("title")]
    public string Title { get; set; } = "Reaparr Torznab";
}

public record TorznabLimits
{
    [XmlAttribute("max")]
    public int Max { get; set; } = 100;

    [XmlAttribute("default")]
    public int Default { get; set; } = 50;
}

public record TorznabSearching
{
    [XmlElement("search")]
    public TorznabSearch Search { get; set; } = new();

    [XmlElement("tv-search")]
    public TorznabSearch TvSearch { get; set; } = new();

    [XmlElement("movie-search")]
    public TorznabSearch MovieSearch { get; set; } = new();
}

public record TorznabSearch
{
    [XmlAttribute("available")]
    public string Available { get; set; } = "yes";

    [XmlAttribute("supportedParams")]
    public string SupportedParams { get; set; } = string.Empty;
}

public record TorznabCategory
{
    public TorznabCategory() { }

    public TorznabCategory(int id, string name)
    {
        Id = id;
        Name = name;
    }

    [XmlAttribute("id")]
    public int Id { get; set; }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

