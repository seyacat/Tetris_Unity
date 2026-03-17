using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Tablero principal de Tetris.
///
/// Configuración pública (desde el Inspector):
///   - blockSprite       : Sprite que se usará para cada bloque
///   - rows / columns    : Dimensiones del tablero en celdas
///   - blockSize         : Tamaño de cada celda en unidades de mundo (ancho y alto)
///   - fallInterval      : Segundos entre caídas automáticas de la pieza
///   - lockDelay         : Segundos de gracia antes de fijar la pieza al tocar el suelo
///
/// Eventos públicos (arrastra tus botones/UI al inspector):
///   - OnGameStarted     : Cuando la partida inicia por primera vez
///   - OnGamePaused      : Cuando se pausa/reanuda
///   - OnGameRestarted   : Cuando se reinicia la partida
///   - OnGameEnded       : Cuando la pieza no cabe al spawnear (game over)
///   - OnLineClear       : Cuando se eliminan líneas (int = cantidad de líneas)
///   - OnScoreChanged    : Cuando cambia el puntaje
/// </summary>
public class TetrisBoard : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════
    //  CONFIGURACIÓN PÚBLICA (Inspector)
    // ════════════════════════════════════════════════════════════════

    [Header("Visuales")]
    [Tooltip("Sprite que se usa para dibujar cada bloque del tablero.")]
    public Sprite blockSprite;

    [Header("Dimensiones del tablero")]
    [Min(4)]  public int rows    = 20;
    [Min(4)]  public int columns = 10;

    [Header("Tamaño de cada bloque (unidades de mundo)")]
    public Vector2 blockSize = Vector2.one;

    [Header("Velocidad")]
    [Tooltip("Segundos entre cada caída automática de la pieza.")]
    [Range(0.05f, 2f)] public float fallInterval = 0.5f;
    [Tooltip("Segundos de gracia antes de bloquear la pieza al tocar el suelo.")]
    [Range(0f, 1f)]    public float lockDelay    = 0.5f;

    // ════════════════════════════════════════════════════════════════
    //  EVENTOS PÚBLICOS
    // ════════════════════════════════════════════════════════════════

    [Header("Eventos")]
    public UnityEvent         OnGameStarted;
    public UnityEvent         OnGamePaused;
    public UnityEvent         OnGameRestarted;
    public UnityEvent         OnGameEnded;
    public UnityEvent<int>    OnLineClear;        // parámetro: líneas eliminadas
    public UnityEvent<int>    OnScoreChanged;     // parámetro: puntaje total

    // ════════════════════════════════════════════════════════════════
    //  ESTADO INTERNO
    // ════════════════════════════════════════════════════════════════

    // Grilla: almacena el SpriteRenderer de cada celda ocupada (null = vacío)
    private SpriteRenderer[,] _grid;

    // Pool de renderers (se crean al inicio y se reutilizan)
    private SpriteRenderer[,] _renderers;

    private TetrisPiece _currentPiece;
    private TetrisPiece _nextPiece;

    private bool _isRunning;
    private bool _isPaused;
    private bool _isGameOver;

    private int  _score;
    private float _fallTimer;
    private float _lockTimer;
    private bool  _pieceGrounded;

    // Puntos por líneas eliminadas de una vez (Guideline)
    private static readonly int[] LinePoints = { 0, 100, 300, 500, 800 };

    // ════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ════════════════════════════════════════════════════════════════

    private void Awake()
    {
        BuildRenderers();
    }

    private void Update()
    {
        if (!_isRunning || _isPaused || _isGameOver) return;

        HandleInput();
        HandleFall();
    }

    // ════════════════════════════════════════════════════════════════
    //  API PÚBLICA  –  Conecta aquí tus botones
    // ════════════════════════════════════════════════════════════════

    /// <summary>Inicia una nueva partida. No hace nada si ya hay una en curso.</summary>
    public void StartGame()
    {
        if (_isRunning) return;
        ResetBoard();
        _isRunning  = true;
        _isPaused   = false;
        _isGameOver = false;
        SpawnNewPiece();
        OnGameStarted?.Invoke();
    }

    /// <summary>Reinicia la partida desde cero, sea cual sea el estado actual.</summary>
    public void RestartGame()
    {
        StopAllCoroutines();
        ResetBoard();
        _isRunning  = true;
        _isPaused   = false;
        _isGameOver = false;
        SpawnNewPiece();
        OnGameRestarted?.Invoke();
    }

    /// <summary>Pausa o reanuda la partida.</summary>
    public void PauseGame()
    {
        if (!_isRunning || _isGameOver) return;
        _isPaused = !_isPaused;
        OnGamePaused?.Invoke();
    }

    /// <summary>Termina la partida manualmente (Game Over forzado).</summary>
    public void EndGame()
    {
        if (!_isRunning) return;
        TriggerGameOver();
    }

    // ════════════════════════════════════════════════════════════════
    //  ESTADO DE LECTURA (útil para UI)
    // ════════════════════════════════════════════════════════════════

    public int  Score     => _score;
    public bool IsRunning => _isRunning;
    public bool IsPaused  => _isPaused;
    public bool IsOver    => _isGameOver;

    // ════════════════════════════════════════════════════════════════
    //  INPUT
    // ════════════════════════════════════════════════════════════════

    private void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame)
            TryMove(Vector2Int.left);

        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            TryMove(Vector2Int.right);

        if (kb.downArrowKey.wasPressedThisFrame  || kb.sKey.wasPressedThisFrame)
        {
            if (!TryMove(Vector2Int.down))
            {
                _pieceGrounded = true;
                _lockTimer     = 0f;
            }
        }

        if (kb.upArrowKey.wasPressedThisFrame    || kb.wKey.wasPressedThisFrame)
            TryRotate(1);

        if (kb.zKey.wasPressedThisFrame)
            TryRotate(-1);

        if (kb.spaceKey.wasPressedThisFrame)
            HardDrop();
    }

    // ════════════════════════════════════════════════════════════════
    //  CAÍDA AUTOMÁTICA
    // ════════════════════════════════════════════════════════════════

    private void HandleFall()
    {
        _fallTimer += Time.deltaTime;

        if (_fallTimer >= fallInterval)
        {
            _fallTimer = 0f;
            if (!TryMove(Vector2Int.down))
            {
                if (_pieceGrounded)
                {
                    _lockTimer += Time.deltaTime;
                    if (_lockTimer >= lockDelay)
                        LockPiece();
                }
                else
                {
                    _pieceGrounded = true;
                    _lockTimer     = 0f;
                }
            }
            else
            {
                _pieceGrounded = false;
                _lockTimer     = 0f;
            }
        }

        // Lock delay independiente del timer de caída
        if (_pieceGrounded)
        {
            _lockTimer += Time.deltaTime;
            if (_lockTimer >= lockDelay)
                LockPiece();
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  MOVIMIENTO Y ROTACIÓN
    // ════════════════════════════════════════════════════════════════

    private bool TryMove(Vector2Int delta)
    {
        var candidate = _currentPiece.WithOffset(delta);
        if (!IsValidPosition(candidate)) return false;

        ErasePiece(_currentPiece);
        _currentPiece = candidate;
        DrawPiece(_currentPiece);
        return true;
    }

    private void TryRotate(int direction)
    {
        var rotated = _currentPiece.WithRotation(direction);
        int kickCount = (_currentPiece.Type == TetrominoType.I) ? 5 : 5;
        int prevRot   = _currentPiece.Rotation;

        var kickTable = (_currentPiece.Type == TetrominoType.I)
            ? TetrisData.WallKicksI
            : TetrisData.WallKicksJLOSTZ;

        for (int k = 0; k < kickCount; k++)
        {
            Vector2Int kick = kickTable[prevRot, k] * direction;
            var candidate   = rotated.WithKick(kick);

            if (IsValidPosition(candidate))
            {
                ErasePiece(_currentPiece);
                _currentPiece = candidate;
                DrawPiece(_currentPiece);

                // Reiniciar lock timer al rotar (T-Spin rule simplificada)
                if (_pieceGrounded) _lockTimer = 0f;
                return;
            }
        }
    }

    private void HardDrop()
    {
        ErasePiece(_currentPiece);
        while (true)
        {
            var next = _currentPiece.WithOffset(Vector2Int.down);
            if (!IsValidPosition(next)) break;
            _currentPiece = next;
        }
        DrawPiece(_currentPiece);
        LockPiece();
    }

    // ════════════════════════════════════════════════════════════════
    //  BLOQUEO Y SPAWN
    // ════════════════════════════════════════════════════════════════

    private void LockPiece()
    {
        // Fijar los colores de la pieza en la grilla permanente
        Color color = TetrisData.Colors[(int)_currentPiece.Type];
        foreach (var cell in _currentPiece.Cells)
        {
            if (IsInBounds(cell))
                _grid[cell.x, cell.y] = _renderers[cell.x, cell.y];
        }

        _pieceGrounded = false;
        _lockTimer     = 0f;
        _fallTimer     = 0f;

        ClearLines();
        SpawnNewPiece();
    }

    private void SpawnNewPiece()
    {
        // Si hay pieza siguiente la usamos; si no, generamos una al azar
        var type = (_nextPiece != null)
            ? _nextPiece.Type
            : RandomType();

        var spawnPos   = new Vector2Int(columns / 2, rows - 2);
        _currentPiece  = new TetrisPiece(type, spawnPos);
        _nextPiece     = new TetrisPiece(RandomType(), spawnPos);

        if (!IsValidPosition(_currentPiece))
        {
            // No hay espacio → game over
            DrawPiece(_currentPiece);   // dibuja aunque se superponga
            TriggerGameOver();
            return;
        }

        DrawPiece(_currentPiece);
    }

    // ════════════════════════════════════════════════════════════════
    //  LÍNEAS
    // ════════════════════════════════════════════════════════════════

    private void ClearLines()
    {
        int linesCleared = 0;
        for (int row = rows - 1; row >= 0; row--)
        {
            if (IsRowFull(row))
            {
                ClearRow(row);
                ShiftRowsDown(row);
                linesCleared++;
                row++;   // revisar la misma fila de nuevo (ahora bajaron)
            }
        }

        if (linesCleared > 0)
        {
            int points = LinePoints[Mathf.Min(linesCleared, 4)];
            _score    += points;
            OnLineClear?.Invoke(linesCleared);
            OnScoreChanged?.Invoke(_score);
        }

        RedrawGrid();
    }

    private bool IsRowFull(int row)
    {
        for (int col = 0; col < columns; col++)
            if (_grid[col, row] == null) return false;
        return true;
    }

    private void ClearRow(int row)
    {
        for (int col = 0; col < columns; col++)
        {
            _grid[col, row] = null;
        }
    }

    private void ShiftRowsDown(int clearedRow)
    {
        for (int row = clearedRow; row < rows - 1; row++)
            for (int col = 0; col < columns; col++)
                _grid[col, row] = _grid[col, row + 1];

        for (int col = 0; col < columns; col++)
            _grid[col, rows - 1] = null;
    }

    // ════════════════════════════════════════════════════════════════
    //  DIBUJO
    // ════════════════════════════════════════════════════════════════

    private void DrawPiece(TetrisPiece piece)
    {
        Color color = TetrisData.Colors[(int)piece.Type];
        foreach (var cell in piece.Cells)
            if (IsInBounds(cell))
                SetRenderer(cell.x, cell.y, color, true);
    }

    private void ErasePiece(TetrisPiece piece)
    {
        foreach (var cell in piece.Cells)
            if (IsInBounds(cell))
            {
                // Solo borrar si NO está fijo en la grilla
                if (_grid[cell.x, cell.y] == null)
                    SetRenderer(cell.x, cell.y, Color.clear, false);
            }
    }

    private void RedrawGrid()
    {
        // Primero limpiar todo
        for (int col = 0; col < columns; col++)
            for (int row = 0; row < rows; row++)
                SetRenderer(col, row, Color.clear, false);

        // Dibujar grilla fija
        for (int col = 0; col < columns; col++)
            for (int row = 0; row < rows; row++)
                if (_grid[col, row] != null)
                {
                    // Recuperar color buscando el tipo… guardamos el color directamente
                    SetRenderer(col, row, _renderers[col, row].color, true);
                }

        // Dibujar pieza activa encima
        if (_currentPiece != null)
            DrawPiece(_currentPiece);
    }

    private void SetRenderer(int col, int row, Color color, bool visible)
    {
        var sr = _renderers[col, row];
        sr.enabled = visible;
        sr.color   = color;
    }

    // ════════════════════════════════════════════════════════════════
    //  CONSTRUCCIÓN DEL POOL DE RENDERERS
    // ════════════════════════════════════════════════════════════════

    private void BuildRenderers()
    {
        _renderers = new SpriteRenderer[columns, rows];
        _grid      = new SpriteRenderer[columns, rows];

        // Origen: centrar el tablero en el GameObject
        float originX = -(columns * blockSize.x) / 2f + blockSize.x / 2f;
        float originY = -(rows    * blockSize.y) / 2f + blockSize.y / 2f;

        for (int col = 0; col < columns; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                var go = new GameObject($"Cell_{col}_{row}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(
                    originX + col * blockSize.x,
                    originY + row * blockSize.y,
                    0f
                );
                go.transform.localScale = new Vector3(blockSize.x, blockSize.y, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite  = blockSprite;
                sr.enabled = false;

                _renderers[col, row] = sr;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  UTILIDADES
    // ════════════════════════════════════════════════════════════════

    private bool IsValidPosition(TetrisPiece piece)
    {
        foreach (var cell in piece.Cells)
        {
            if (!IsInBounds(cell))           return false;
            if (_grid[cell.x, cell.y] != null) return false;
        }
        return true;
    }

    private bool IsInBounds(Vector2Int cell) =>
        cell.x >= 0 && cell.x < columns &&
        cell.y >= 0 && cell.y < rows;

    private void ResetBoard()
    {
        _score         = 0;
        _fallTimer     = 0f;
        _lockTimer     = 0f;
        _pieceGrounded = false;
        _currentPiece  = null;
        _nextPiece     = null;

        if (_grid == null)
        {
            _grid      = new SpriteRenderer[columns, rows];
            BuildRenderers();
        }

        for (int col = 0; col < columns; col++)
            for (int row = 0; row < rows; row++)
            {
                _grid[col, row] = null;
                SetRenderer(col, row, Color.clear, false);
            }
    }

    private void TriggerGameOver()
    {
        _isRunning  = false;
        _isGameOver = true;
        OnGameEnded?.Invoke();
    }

    private static TetrominoType RandomType()
    {
        var values = System.Enum.GetValues(typeof(TetrominoType));
        return (TetrominoType)values.GetValue(Random.Range(0, values.Length));
    }

    // ════════════════════════════════════════════════════════════════
    //  GIZMOS (ayuda visual en la escena)
    // ════════════════════════════════════════════════════════════════

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
        float originX = -(columns * blockSize.x) / 2f;
        float originY = -(rows    * blockSize.y) / 2f;
        Vector3 size  = new Vector3(columns * blockSize.x, rows * blockSize.y, 0f);
        Vector3 center = transform.position + new Vector3(
            originX + size.x / 2f,
            originY + size.y / 2f,
            0f);
        Gizmos.DrawWireCube(center, size);
    }
}
