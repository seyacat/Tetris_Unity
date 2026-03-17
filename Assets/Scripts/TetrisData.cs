using UnityEngine;

/// <summary>
/// Tipos de piezas de Tetris (tetrominós estándar).
/// </summary>
public enum TetrominoType
{
    I, O, T, S, Z, J, L
}

/// <summary>
/// Datos estáticos de cada pieza: forma de las celdas y color asociado.
/// </summary>
public static class TetrisData
{
    // Offset central de cada pieza respecto a su celda de origen (columna spawn)
    // Cada Vector2Int es una celda relativa al pivot de la pieza
    public static readonly Vector2Int[][] Cells = new Vector2Int[][]
    {
        // I
        new Vector2Int[] { new(-1,1), new(0,1), new(1,1), new(2,1) },
        // O
        new Vector2Int[] { new(0,0), new(1,0), new(0,1), new(1,1) },
        // T
        new Vector2Int[] { new(-1,0), new(0,0), new(1,0), new(0,1) },
        // S
        new Vector2Int[] { new(0,0), new(1,0), new(-1,1), new(0,1) },
        // Z
        new Vector2Int[] { new(-1,0), new(0,0), new(0,1), new(1,1) },
        // J
        new Vector2Int[] { new(-1,1), new(-1,0), new(0,0), new(1,0) },
        // L
        new Vector2Int[] { new(1,1), new(-1,0), new(0,0), new(1,0) },
    };

    // Colores estándar Tetris Guideline
    public static readonly Color[] Colors = new Color[]
    {
        new Color(0.00f, 0.89f, 0.96f), // I - Cyan
        new Color(1.00f, 0.85f, 0.00f), // O - Yellow
        new Color(0.60f, 0.00f, 1.00f), // T - Purple
        new Color(0.00f, 0.78f, 0.14f), // S - Green
        new Color(1.00f, 0.15f, 0.15f), // Z - Red
        new Color(0.00f, 0.27f, 1.00f), // J - Blue
        new Color(1.00f, 0.55f, 0.00f), // L - Orange
    };

    // Wall kick data (SRS - Super Rotation System)
    // [rotación actual][intento de kick] -> offset a probar
    public static readonly Vector2Int[,] WallKicksJLOSTZ =
    {
        { new(0,0), new(-1,0), new(-1,1), new(0,-2), new(-1,-2) }, // 0→1
        { new(0,0), new(1,0),  new(1,-1), new(0,2),  new(1,2)  }, // 1→2
        { new(0,0), new(1,0),  new(1,1),  new(0,-2), new(1,-2) }, // 2→3
        { new(0,0), new(-1,0), new(-1,-1),new(0,2),  new(-1,2) }, // 3→0
    };

    public static readonly Vector2Int[,] WallKicksI =
    {
        { new(0,0), new(-2,0), new(1,0),  new(-2,-1), new(1,2)  }, // 0→1
        { new(0,0), new(-1,0), new(2,0),  new(-1,2),  new(2,-1) }, // 1→2
        { new(0,0), new(2,0),  new(-1,0), new(2,1),   new(-1,-2)}, // 2→3
        { new(0,0), new(1,0),  new(-2,0), new(1,-2),  new(-2,1) }, // 3→0
    };
}
