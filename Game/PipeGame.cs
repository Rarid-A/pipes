using System.Collections.Generic;

namespace PipesPuzzle.Game;

[Flags]
public enum Direction
{
    None = 0,
    Up = 1,
    Right = 2,
    Down = 4,
    Left = 8
}

public enum TileKind
{
    Valve,
    End,
    Straight,
    Corner,
    Tee
}

public sealed class PipeTile
{
    public required TileKind Kind { get; init; }
    public required Direction SolutionOpenings { get; init; }
    public bool IsValve { get; init; }
    public int Rotation { get; set; }
    public bool IsWet { get; set; }

    public Direction CurrentOpenings => IsValve
        ? SolutionOpenings
        : PipeMath.Rotate(SolutionOpenings, Rotation);
}

public sealed class PipeGameBoard
{
    private readonly Random _rng;

    public PipeGameBoard(Random rng)
    {
        _rng = rng;
    }

    public int Size { get; private set; }
    public PipeTile[,] Tiles { get; private set; } = new PipeTile[0, 0];
    public int ValveRow => Size / 2;
    public int ValveCol => Size / 2;
    public bool WaterReleased => true;

    public void NewPuzzle(int size)
    {
        if (size < 4 || size > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Grid size must be between 4 and 25.");
        }

        Size = size;
        Tiles = new PipeTile[size, size];
        var openings = BuildTreeAsOpenings(size);

        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                var isValve = r == ValveRow && c == ValveCol;
                var tileOpenings = openings[r, c];
                var kind = isValve ? TileKind.Valve : PipeMath.KindFromOpenings(tileOpenings);
                var rotation = isValve ? 0 : _rng.Next(0, 4);

                Tiles[r, c] = new PipeTile
                {
                    Kind = kind,
                    SolutionOpenings = tileOpenings,
                    IsValve = isValve,
                    Rotation = rotation,
                    IsWet = false
                };
            }
        }

        UpdateWater();
    }

    public bool RotateAt(int row, int col)
    {
        if (!InBounds(row, col))
        {
            return false;
        }

        var tile = Tiles[row, col];
        if (tile.IsValve)
        {
            return false;
        }

        tile.Rotation = (tile.Rotation + 1) % 4;
        UpdateWater();
        return true;
    }

    public void SetWaterReleased(bool released)
    {
        // Water is always on in this game mode.
        UpdateWater();
    }

    public bool IsSolved()
    {
        if (Size == 0)
        {
            return false;
        }

        var edgeCount = 0;
        var visited = new bool[Size, Size];
        var queue = new Queue<(int row, int col)>();

        queue.Enqueue((0, 0));
        visited[0, 0] = true;
        var connectedCount = 0;

        while (queue.Count > 0)
        {
            var (row, col) = queue.Dequeue();
            connectedCount++;
            var openings = Tiles[row, col].CurrentOpenings;

            foreach (var dir in PipeMath.AllDirections)
            {
                if (!openings.HasFlag(dir))
                {
                    continue;
                }

                var (nr, nc) = PipeMath.Step(row, col, dir);
                if (!InBounds(nr, nc))
                {
                    return false;
                }

                var neighborOpenings = Tiles[nr, nc].CurrentOpenings;
                var opposite = PipeMath.Opposite(dir);
                if (!neighborOpenings.HasFlag(opposite))
                {
                    return false;
                }

                if ((dir == Direction.Right) || (dir == Direction.Down))
                {
                    edgeCount++;
                }

                if (visited[nr, nc])
                {
                    continue;
                }

                visited[nr, nc] = true;
                queue.Enqueue((nr, nc));
            }
        }

        var nodeCount = Size * Size;
        return connectedCount == nodeCount && edgeCount == nodeCount - 1;
    }

    public void UpdateWater()
    {
        for (var r = 0; r < Size; r++)
        {
            for (var c = 0; c < Size; c++)
            {
                Tiles[r, c].IsWet = false;
            }
        }

        if (Size == 0)
        {
            return;
        }

        var visited = new bool[Size, Size];
        var queue = new Queue<(int row, int col)>();
        queue.Enqueue((ValveRow, ValveCol));
        visited[ValveRow, ValveCol] = true;

        while (queue.Count > 0)
        {
            var (row, col) = queue.Dequeue();
            Tiles[row, col].IsWet = true;
            var openings = Tiles[row, col].CurrentOpenings;

            foreach (var dir in PipeMath.AllDirections)
            {
                if (!openings.HasFlag(dir))
                {
                    continue;
                }

                var (nr, nc) = PipeMath.Step(row, col, dir);
                if (!InBounds(nr, nc) || visited[nr, nc])
                {
                    continue;
                }

                var opposite = PipeMath.Opposite(dir);
                var neighborOpenings = Tiles[nr, nc].CurrentOpenings;
                if (!neighborOpenings.HasFlag(opposite))
                {
                    continue;
                }

                visited[nr, nc] = true;
                queue.Enqueue((nr, nc));
            }
        }
    }

    private bool InBounds(int row, int col)
    {
        return row >= 0 && col >= 0 && row < Size && col < Size;
    }

    private Direction[,] BuildTreeAsOpenings(int size)
    {
        // The board is generated as a degree-limited spanning tree (single connected group, no loops).
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var result = TryBuildOpenings(size);
            if (result is not null)
            {
                return result;
            }
        }

        throw new InvalidOperationException("Unable to generate a valid puzzle. Please try again.");
    }

    private Direction[,]? TryBuildOpenings(int size)
    {
        var nodeCount = size * size;
        var uf = new UnionFind(nodeCount);
        var degrees = new int[nodeCount];
        var openings = new Direction[size, size];

        var edges = new List<(int ar, int ac, int br, int bc, Direction dir)>(size * size * 2);
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                if (r + 1 < size)
                {
                    edges.Add((r, c, r + 1, c, Direction.Down));
                }

                if (c + 1 < size)
                {
                    edges.Add((r, c, r, c + 1, Direction.Right));
                }
            }
        }

        PipeMath.Shuffle(edges, _rng);

        var chosenEdges = 0;
        foreach (var edge in edges)
        {
            var a = edge.ar * size + edge.ac;
            var b = edge.br * size + edge.bc;
            if (uf.Find(a) == uf.Find(b))
            {
                continue;
            }

            var maxDegreeA = MaxDegree(edge.ar, edge.ac, size);
            var maxDegreeB = MaxDegree(edge.br, edge.bc, size);
            if (degrees[a] >= maxDegreeA || degrees[b] >= maxDegreeB)
            {
                continue;
            }

            uf.Union(a, b);
            degrees[a]++;
            degrees[b]++;
            chosenEdges++;

            openings[edge.ar, edge.ac] |= edge.dir;
            openings[edge.br, edge.bc] |= PipeMath.Opposite(edge.dir);

            if (chosenEdges == nodeCount - 1)
            {
                break;
            }
        }

        if (chosenEdges != nodeCount - 1)
        {
            return null;
        }

        var center = (size / 2) * size + (size / 2);
        if (degrees[center] < 2)
        {
            return null;
        }

        for (var i = 0; i < nodeCount; i++)
        {
            if (degrees[i] == 0)
            {
                return null;
            }

            var row = i / size;
            var col = i % size;
            if ((row != size / 2 || col != size / 2) && PipeMath.PopCount(openings[row, col]) == 4)
            {
                return null;
            }
        }

        return openings;
    }

    private int MaxDegree(int row, int col, int size)
    {
        return row == size / 2 && col == size / 2 ? 4 : 3;
    }
}

internal static class PipeMath
{
    public static readonly Direction[] AllDirections = [Direction.Up, Direction.Right, Direction.Down, Direction.Left];

    public static Direction Rotate(Direction direction, int stepsClockwise)
    {
        var result = direction;
        var steps = ((stepsClockwise % 4) + 4) % 4;
        for (var i = 0; i < steps; i++)
        {
            var next = Direction.None;
            if (result.HasFlag(Direction.Up))
            {
                next |= Direction.Right;
            }

            if (result.HasFlag(Direction.Right))
            {
                next |= Direction.Down;
            }

            if (result.HasFlag(Direction.Down))
            {
                next |= Direction.Left;
            }

            if (result.HasFlag(Direction.Left))
            {
                next |= Direction.Up;
            }

            result = next;
        }

        return result;
    }

    public static int PopCount(Direction direction)
    {
        var value = (int)direction;
        var count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }

    public static TileKind KindFromOpenings(Direction openings)
    {
        var degree = PopCount(openings);
        return degree switch
        {
            1 => TileKind.End,
            2 when IsOppositePair(openings) => TileKind.Straight,
            2 => TileKind.Corner,
            3 => TileKind.Tee,
            _ => TileKind.End
        };
    }

    public static bool IsOppositePair(Direction openings)
    {
        return openings == (Direction.Up | Direction.Down) || openings == (Direction.Left | Direction.Right);
    }

    public static (int row, int col) Step(int row, int col, Direction direction)
    {
        return direction switch
        {
            Direction.Up => (row - 1, col),
            Direction.Right => (row, col + 1),
            Direction.Down => (row + 1, col),
            Direction.Left => (row, col - 1),
            _ => (row, col)
        };
    }

    public static Direction Opposite(Direction direction)
    {
        return direction switch
        {
            Direction.Up => Direction.Down,
            Direction.Right => Direction.Left,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            _ => Direction.None
        };
    }

    public static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

internal sealed class UnionFind
{
    private readonly int[] _parent;
    private readonly int[] _rank;

    public UnionFind(int size)
    {
        _parent = new int[size];
        _rank = new int[size];
        for (var i = 0; i < size; i++)
        {
            _parent[i] = i;
            _rank[i] = 0;
        }
    }

    public int Find(int x)
    {
        if (_parent[x] != x)
        {
            _parent[x] = Find(_parent[x]);
        }

        return _parent[x];
    }

    public void Union(int a, int b)
    {
        var rootA = Find(a);
        var rootB = Find(b);
        if (rootA == rootB)
        {
            return;
        }

        if (_rank[rootA] < _rank[rootB])
        {
            _parent[rootA] = rootB;
        }
        else if (_rank[rootA] > _rank[rootB])
        {
            _parent[rootB] = rootA;
        }
        else
        {
            _parent[rootB] = rootA;
            _rank[rootA]++;
        }
    }
}
