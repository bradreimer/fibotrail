using Fibotrail.Trails;

namespace Fibotrail.Server.Trails;

/// <summary>
/// The full points of the last few trail files read, split at gaps. Aligning photos needs every
/// point with its time, which only the kept file has, so the trip page would read a file again for
/// every request without this. A stored file never changes, so an entry is never stale.
/// </summary>
public sealed class TrailFileCache
{
    public const int Capacity = 8;

    private readonly Lock _gate = new();
    private readonly LinkedList<(string Path, IReadOnlyList<IReadOnlyList<TrailPoint>> Segments)> _entries = new();

    /// <summary>The points of a kept file, split at gaps. Null when the file is missing or can't be read.</summary>
    public IReadOnlyList<IReadOnlyList<TrailPoint>>? Get(string path)
    {
        lock (_gate)
        {
            for (var node = _entries.First; node is not null; node = node.Next)
            {
                if (node.Value.Path == path)
                {
                    _entries.Remove(node);
                    _entries.AddFirst(node);
                    return node.Value.Segments;
                }
            }
        }

        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        var parsed = GpxParser.Parse(stream);
        if (parsed.Trail is null)
        {
            return null;
        }

        var segments = TrailMath.SplitAtGaps(parsed.Trail.Segments);
        lock (_gate)
        {
            _entries.AddFirst((path, segments));
            while (_entries.Count > Capacity)
            {
                _entries.RemoveLast();
            }
        }

        return segments;
    }
}
