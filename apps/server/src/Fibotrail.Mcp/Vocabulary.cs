namespace Fibotrail.Mcp;

/// <summary>
/// The kinds of value a tool result may hold. This is the vocabulary of ADR-018: counts, dates,
/// country, continent, collection and park names, trip IDs, and kilometers. A field of any other
/// kind doesn't exist, so a result can't hold a coordinate, a photo, or a trip name by accident.
/// </summary>
public enum Coarse
{
    /// <summary>A count or a number of days.</summary>
    Count,

    /// <summary>A date, to the day.</summary>
    Date,

    /// <summary>The name of a country.</summary>
    Country,

    /// <summary>The name of a continent.</summary>
    Continent,

    /// <summary>The name of a collection, a resort, or a park.</summary>
    Place,

    /// <summary>The ID of a collection or a country, such as <c>disney-parks</c> or <c>752</c>.</summary>
    Identifier,

    /// <summary>The ID of an added trip.</summary>
    TripId,

    /// <summary>A distance in kilometers.</summary>
    DistanceKm,

    /// <summary>A yes or no.</summary>
    Flag,

    /// <summary>A fixed sentence that the code writes, never a value from your data.</summary>
    Note,
}

/// <summary>Declares the kind of a result field. A result field with no kind fails a test.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CoarseAttribute(Coarse kind) : Attribute
{
    public Coarse Kind { get; } = kind;
}

/// <summary>Marks a result field that holds another result record, or a list of them.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NestedAttribute : Attribute;
