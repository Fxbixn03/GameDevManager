namespace GameDevManager.Domain.Entities;

/// <summary>Verbindet ein Asset mit einem Stichwort. Schlüssel ist das Paar aus beiden.</summary>
public class AssetTagAssignment
{
    public Guid AssetId { get; set; }

    public Asset? Asset { get; set; }

    public Guid AssetTagId { get; set; }

    public AssetTag? Tag { get; set; }
}
