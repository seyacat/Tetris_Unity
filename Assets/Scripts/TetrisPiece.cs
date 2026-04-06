using UnityEngine;

/// <summary>
/// Representa la pieza activa en el tablero.
/// Gestiona sus celdas mundiales, su rotación y el tipo que es.
/// </summary>
public class TetrisPiece
{
    // ── Datos de la pieza ────────────────────────────────────────────
    public TetrominoType Type     { get; private set; }
    public Vector2Int    Position { get; private set; }   // pivot en coordenadas del tablero
    public int           Rotation { get; private set; }   // 0..3

    // Celdas en coordenadas del tablero (se recalculan al mover/rotar)
    public Vector2Int[] Cells { get; private set; }

    // ── Constructor ─────────────────────────────────────────────────
    public TetrisPiece(TetrominoType type, Vector2Int spawnPosition)
    {
        Type     = type;
        Position = spawnPosition;
        Rotation = 0;
        Cells    = new Vector2Int[TetrisData.Cells[(int)type].Length];
        RecalculateCells();
    }

    // ── Movimiento ──────────────────────────────────────────────────

    /// <summary>Devuelve una copia desplazada sin modificar la original.</summary>
    public TetrisPiece WithOffset(Vector2Int delta)
    {
        var copy = Clone();
        copy.Position += delta;
        copy.RecalculateCells();
        return copy;
    }

    /// <summary>Devuelve una copia con rotación aplicada (SRS).</summary>
    public TetrisPiece WithRotation(int direction)
    {
        var copy = Clone();
        copy.Rotation = ((copy.Rotation + direction) % 4 + 4) % 4;
        copy.RecalculateCells();
        return copy;
    }

    /// <summary>Aplica un offset de wall-kick a la copia.</summary>
    public TetrisPiece WithKick(Vector2Int kick)
    {
        var copy = Clone();
        copy.Position += kick;
        copy.RecalculateCells();
        return copy;
    }

    // ── Internos ─────────────────────────────────────────────────────
    private void RecalculateCells()
    {
        Vector2Int[] template = TetrisData.Cells[(int)Type];
        for (int i = 0; i < template.Length; i++)
            Cells[i] = Position + Rotate(template[i], Rotation);
    }

    private static Vector2Int Rotate(Vector2Int cell, int rotation)
    {
        return rotation switch
        {
            0 => cell,
            1 => new Vector2Int(cell.y, -cell.x),
            2 => new Vector2Int(-cell.x, -cell.y),
            3 => new Vector2Int(-cell.y, cell.x),
            _ => cell,
        };
    }

    private TetrisPiece Clone()
    {
        var c = new TetrisPiece(Type, Position);
        c.Rotation = Rotation;
        c.RecalculateCells();
        return c;
    }
}
