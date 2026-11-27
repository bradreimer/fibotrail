using System.Globalization;
using System.Text;

namespace Fibotrail.Games.Pin;

/// <summary>How far a pin is from the answer.</summary>
public enum PinBand
{
    /// <summary>50 km or less. Solved. 🟩</summary>
    Green,

    /// <summary>Over 50 km, up to 500 km. 🟨</summary>
    Yellow,

    /// <summary>Over 500 km, up to 2,000 km. 🟧</summary>
    Orange,

    /// <summary>Over 2,000 km. 🟥</summary>
    Red,
}

/// <summary>One of the 8 compass points, from a pin toward the answer.</summary>
public enum Compass
{
    N,
    NE,
    E,
    SE,
    S,
    SW,
    W,
    NW,
}

/// <summary>A point on the map, in degrees.</summary>
public sealed record GeoPoint(double Latitude, double Longitude)
{
    /// <summary>True when the latitude is from -90 to 90 and the longitude from -180 to 180.</summary>
    public bool IsValid =>
        double.IsFinite(Latitude) && double.IsFinite(Longitude) &&
        Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180;

    /// <summary>
    /// The point rounded to 4 decimals, about 11 m. Pins are stored this way, so a stored pin
    /// grades the same as the pin that was sent.
    /// </summary>
    public GeoPoint Rounded() => new(Math.Round(Latitude, 4), Math.Round(Longitude, 4));
}

/// <summary>The grade of one pin.</summary>
/// <param name="Direction">The way to go from the pin, or null when solved.</param>
/// <param name="DistanceKm">The distance, rounded up: to 10 km below 1,000 km, and to 100 km from there.</param>
public sealed record PinMark(GeoPoint Pin, PinBand Band, Compass? Direction, int DistanceKm)
{
    public bool Solved => Band == PinBand.Green;
}

/// <summary>The graded pins of a whole game.</summary>
public sealed record PinOutcome(bool Solved, IReadOnlyList<PinMark> Marks);

/// <summary>The photo of a puzzle, its trip, and where it was taken.</summary>
/// <param name="Place">The trip's main destination, or null when none is known.</param>
public sealed record PinPhoto(string PhotoId, GeoPoint Location, int TripId, string TripName, DateOnly StartDate, DateOnly EndDate, string? Place);

/// <summary>
/// The Pin game: one trip photo a day. You place a pin on the map where you think you took it, in
/// up to 4 tries, and each miss shows a distance band and the way to go. Pure functions over the
/// game library. See docs/specs/pin-game.md.
/// </summary>
public sealed record PinPuzzle(DateOnly Date, int Number, PinPhoto Photo)
{
    public const string Id = "pin";
    public const string Name = "Pin";
    public const int MaxTries = 4;

    /// <summary>The mean radius of the Earth, for the haversine distance.</summary>
    public const double EarthRadiusKm = 6371;

    /// <summary>A pin this close or closer solves the puzzle.</summary>
    public const double SolvedKm = 50;

    /// <summary>The upper ends of Yellow and Orange.</summary>
    public const double CloseKm = 500;
    public const double RegionKm = 2000;

    /// <summary>How many more trips with a located photo the library needs: 1 or 0.</summary>
    public static int TripsNeeded(GameLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return library.Trips.Any(trip => trip.Photos.Any(photo => photo.HasLocation)) ? 0 : 1;
    }

    /// <summary>The puzzle of a date, or null when no added trip has a photo with a location.</summary>
    public static PinPuzzle? Create(GameLibrary library, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(library);
        var random = new SeededRandom(DailySeed.For(Id, date));

        // Sort first, so the picks don't depend on the order the database returned.
        var eligible = library.Trips
            .Select(trip => (Trip: trip, Photos: trip.Photos
                .Where(photo => photo.HasLocation)
                .OrderBy(photo => photo.LocalTime)
                .ThenBy(photo => photo.Id, StringComparer.Ordinal)
                .ToList()))
            .Where(pick => pick.Photos.Count > 0)
            .OrderBy(pick => pick.Trip.Id)
            .ToList();
        if (eligible.Count == 0)
        {
            return null;
        }

        var (trip, photos) = eligible[random.Next(eligible.Count)];
        var photo = photos[random.Next(photos.Count)];
        return new PinPuzzle(
            date,
            DailySeed.PuzzleNumber(date),
            new PinPhoto(photo.Id, new GeoPoint(photo.Latitude!.Value, photo.Longitude!.Value), trip.Id, trip.Name, trip.StartDate, trip.EndDate, trip.Place));
    }

    /// <summary>The great-circle distance in km, by the haversine formula.</summary>
    public static double DistanceKm(GeoPoint from, GeoPoint to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        var lat1 = Radians(from.Latitude);
        var lat2 = Radians(to.Latitude);
        var dLat = lat2 - lat1;
        var dLon = Radians(to.Longitude - from.Longitude);
        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) + (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>The initial bearing from one point to another, in degrees clockwise from north, 0 to 360.</summary>
    public static double BearingDegrees(GeoPoint from, GeoPoint to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        var lat1 = Radians(from.Latitude);
        var lat2 = Radians(to.Latitude);
        var dLon = Radians(to.Longitude - from.Longitude);
        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = (Math.Cos(lat1) * Math.Sin(lat2)) - (Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon));
        var degrees = Math.Atan2(y, x) * 180 / Math.PI;
        return (degrees + 360) % 360;
    }

    /// <summary>The nearest of the 8 compass points, each 45° wide.</summary>
    public static Compass CompassOf(double bearingDegrees) =>
        (Compass)((int)Math.Round(((bearingDegrees % 360) + 360) % 360 / 45, MidpointRounding.AwayFromZero) % 8);

    public static PinBand BandOf(double distanceKm) => distanceKm switch
    {
        <= SolvedKm => PinBand.Green,
        <= CloseKm => PinBand.Yellow,
        <= RegionKm => PinBand.Orange,
        _ => PinBand.Red,
    };

    /// <summary>
    /// The distance as shown: rounded up to 10 km below 1,000 km, and to 100 km from there. Rounding
    /// up keeps a miss of 51 km from showing as "50 km".
    /// </summary>
    public static int ShownKm(double distanceKm)
    {
        var step = distanceKm < 1000 ? 10 : 100;
        return (int)(Math.Ceiling(distanceKm / step) * step);
    }

    /// <summary>Grades one pin, or null when it's off the map.</summary>
    public PinMark? Grade(GeoPoint pin)
    {
        if (pin is null || !pin.IsValid)
        {
            return null;
        }

        var placed = pin.Rounded();
        var distance = DistanceKm(placed, Photo.Location);
        var band = BandOf(distance);
        return new PinMark(
            placed,
            band,
            band == PinBand.Green ? null : CompassOf(BearingDegrees(placed, Photo.Location)),
            ShownKm(distance));
    }

    /// <summary>
    /// Grades every pin of a game. Null when there are none, more than <see cref="MaxTries"/>, one is
    /// off the map, or one comes after a solved pin.
    /// </summary>
    public PinOutcome? Score(IReadOnlyList<GeoPoint> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        if (pins.Count is 0 or > MaxTries)
        {
            return null;
        }

        var marks = new List<PinMark>();
        foreach (var pin in pins)
        {
            if (marks.Count > 0 && marks[^1].Solved)
            {
                return null;
            }

            if (Grade(pin) is not { } mark)
            {
                return null;
            }

            marks.Add(mark);
        }

        return new PinOutcome(marks[^1].Solved, marks);
    }

    /// <summary>The pins as stored text: "latitude longitude" with 4 decimals, joined by ";".</summary>
    public static string Encode(IEnumerable<GeoPoint> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        return string.Join(';', pins.Select(pin => string.Create(CultureInfo.InvariantCulture, $"{pin.Rounded().Latitude:0.####} {pin.Rounded().Longitude:0.####}")));
    }

    public static IReadOnlyList<GeoPoint> Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(row =>
        {
            var parts = row.Split(' ');
            return new GeoPoint(double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture));
        })];
    }

    /// <summary>
    /// The text you paste to brag: the game, the puzzle number, the tries (X when not solved), and
    /// one line per try of a square and, for a miss, an arrow. It holds no distance, place, trip
    /// name, date, or photo.
    /// </summary>
    public static string ShareText(int puzzleNumber, PinOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Fibotrail {Name} #{puzzleNumber} {(outcome.Solved ? outcome.Marks.Count.ToString(CultureInfo.InvariantCulture) : "X")}/{MaxTries}");
        foreach (var mark in outcome.Marks)
        {
            text.Append('\n').Append(Square(mark.Band));
            if (mark.Direction is { } direction)
            {
                text.Append(Arrow(direction));
            }
        }

        return text.ToString();
    }

    public static string Square(PinBand band) => band switch
    {
        PinBand.Green => "🟩",
        PinBand.Yellow => "🟨",
        PinBand.Orange => "🟧",
        _ => "🟥",
    };

    public static string Arrow(Compass direction) => direction switch
    {
        Compass.N => "⬆️",
        Compass.NE => "↗️",
        Compass.E => "➡️",
        Compass.SE => "↘️",
        Compass.S => "⬇️",
        Compass.SW => "↙️",
        Compass.W => "⬅️",
        _ => "↖️",
    };

    private static double Radians(double degrees) => degrees * Math.PI / 180;
}
