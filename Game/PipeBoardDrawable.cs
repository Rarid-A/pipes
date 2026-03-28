namespace PipesPuzzle.Game;

public sealed class PipeBoardDrawable : IDrawable
{
    public PipeGameBoard? Board { get; set; }
    public float CellSize { get; set; } = 36f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        canvas.FillColor = Color.FromArgb("#DDDDDD");
        canvas.FillRectangle(dirtyRect);

        var board = Board;
        if (board is null || board.Size <= 0)
        {
            canvas.RestoreState();
            return;
        }

        var outerStroke = MathF.Max(5f, CellSize * 0.22f);
        var innerStroke = MathF.Max(2f, CellSize * 0.11f);
        var centerRadiusOuter = MathF.Max(4f, CellSize * 0.14f);
        var centerRadiusInner = MathF.Max(2f, CellSize * 0.08f);
        var valveRadius = MathF.Max(8f, CellSize * 0.3f);

        DrawGrid(canvas, board.Size, CellSize);

        for (var r = 0; r < board.Size; r++)
        {
            for (var c = 0; c < board.Size; c++)
            {
                var tile = board.Tiles[r, c];
                var x = c * CellSize;
                var y = r * CellSize;
                DrawPipe(canvas, tile, x, y, CellSize, outerStroke, innerStroke, centerRadiusOuter, centerRadiusInner);

                if (tile.IsValve)
                {
                    DrawValve(canvas, x, y, CellSize, valveRadius, tile.IsWet);
                }
                else if (tile.Kind == TileKind.End)
                {
                    DrawStopper(canvas, tile, x, y, CellSize);
                }
            }
        }

        canvas.RestoreState();
    }

    private static void DrawGrid(ICanvas canvas, int size, float cellSize)
    {
        var boardPx = size * cellSize;
        canvas.StrokeColor = Color.FromArgb("#BEBEBE");
        canvas.StrokeSize = 1f;

        for (var i = 0; i <= size; i++)
        {
            var p = i * cellSize;
            canvas.DrawLine(p, 0, p, boardPx);
            canvas.DrawLine(0, p, boardPx, p);
        }
    }

    private static void DrawPipe(ICanvas canvas, PipeTile tile, float x, float y, float size, float outerStroke, float innerStroke, float centerRadiusOuter, float centerRadiusInner)
    {
        var cx = x + (size / 2f);
        var cy = y + (size / 2f);
        var innerColor = tile.IsWet ? Color.FromArgb("#8FC8FF") : Color.FromArgb("#FFFFFF");

        canvas.StrokeColor = Color.FromArgb("#000000");
        canvas.StrokeSize = outerStroke;
        canvas.StrokeLineCap = LineCap.Round;

        var openings = tile.CurrentOpenings;

        if (openings.HasFlag(Direction.Up))
        {
            canvas.DrawLine(cx, cy, cx, y + 3f);
        }

        if (openings.HasFlag(Direction.Right))
        {
            canvas.DrawLine(cx, cy, x + size - 3f, cy);
        }

        if (openings.HasFlag(Direction.Down))
        {
            canvas.DrawLine(cx, cy, cx, y + size - 3f);
        }

        if (openings.HasFlag(Direction.Left))
        {
            canvas.DrawLine(cx, cy, x + 3f, cy);
        }

        canvas.StrokeColor = innerColor;
        canvas.StrokeSize = innerStroke;

        if (openings.HasFlag(Direction.Up))
        {
            canvas.DrawLine(cx, cy, cx, y + 3f);
        }

        if (openings.HasFlag(Direction.Right))
        {
            canvas.DrawLine(cx, cy, x + size - 3f, cy);
        }

        if (openings.HasFlag(Direction.Down))
        {
            canvas.DrawLine(cx, cy, cx, y + size - 3f);
        }

        if (openings.HasFlag(Direction.Left))
        {
            canvas.DrawLine(cx, cy, x + 3f, cy);
        }

        canvas.FillColor = Color.FromArgb("#000000");
        canvas.FillCircle(cx, cy, centerRadiusOuter);
        canvas.FillColor = innerColor;
        canvas.FillCircle(cx, cy, centerRadiusInner);
    }

    private static void DrawValve(ICanvas canvas, float x, float y, float size, float radius, bool isWet)
    {
        var cx = x + (size / 2f);
        var cy = y + (size / 2f);

        canvas.FillColor = Color.FromArgb("#D7263D");
        canvas.FillCircle(cx, cy, radius);

        canvas.StrokeColor = Color.FromArgb("#000000");
        canvas.StrokeSize = MathF.Max(2f, size * 0.08f);
        canvas.DrawCircle(cx, cy, radius);

        canvas.StrokeColor = isWet ? Color.FromArgb("#9DD3FF") : Color.FromArgb("#F4C1C7");
        canvas.StrokeSize = MathF.Max(2f, size * 0.06f);
        canvas.DrawLine(cx - radius * 0.45f, cy, cx + radius * 0.45f, cy);
        canvas.DrawLine(cx, cy - radius * 0.45f, cx, cy + radius * 0.45f);
    }

    private static void DrawStopper(ICanvas canvas, PipeTile tile, float x, float y, float size)
    {
        var openings = tile.CurrentOpenings;
        var blockedDirection = Direction.None;

        if (openings.HasFlag(Direction.Up))
        {
            blockedDirection = Direction.Down;
        }
        else if (openings.HasFlag(Direction.Right))
        {
            blockedDirection = Direction.Left;
        }
        else if (openings.HasFlag(Direction.Down))
        {
            blockedDirection = Direction.Up;
        }
        else if (openings.HasFlag(Direction.Left))
        {
            blockedDirection = Direction.Right;
        }

        var cx = x + (size / 2f);
        var cy = y + (size / 2f);
        var r = MathF.Max(2f, size * 0.09f);

        var (sx, sy) = blockedDirection switch
        {
            Direction.Up => (cx, y + size * 0.2f),
            Direction.Right => (x + size * 0.8f, cy),
            Direction.Down => (cx, y + size * 0.8f),
            Direction.Left => (x + size * 0.2f, cy),
            _ => (cx, cy)
        };

        canvas.FillColor = tile.IsWet ? Color.FromArgb("#B7DEFF") : Color.FromArgb("#FFFFFF");
        canvas.FillCircle(sx, sy, r);
        canvas.StrokeColor = Color.FromArgb("#000000");
        canvas.StrokeSize = MathF.Max(1.5f, size * 0.04f);
        canvas.DrawCircle(sx, sy, r);
    }
}
