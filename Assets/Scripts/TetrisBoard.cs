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
    [Tooltip("9 sprites en orden: I, O, T, S, Z, J, L, BigT, Block2x3. Si un slot queda vacío se usa el primero.")]
    public Sprite[] pieceSprites = new Sprite[9];

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

    // Colores y sprites fijos de la grilla (paralelos a _grid)
    private Color[,]  _gridColors;
    private Sprite[,] _gridSprites;

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

    // Tracking de touch / mouse
    private Vector2 _pointerStartPos;
    private Vector2 _dragLastPos;
    private bool    _isDraggingPointer;
    private bool    _prevPointerPressed;   // estado del frame anterior para detectar edge

    // ════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (_renderers == null)
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
    public TetrisPiece NextPiece => _nextPiece;

    // ════════════════════════════════════════════════════════════════
    //  INPUT
    // ════════════════════════════════════════════════════════════════

    private void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame)
                TryMove(Vector2Int.left);

            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                TryMove(Vector2Int.right);

            if (kb.downArrowKey.wasPressedThisFrame  || kb.sKey.wasPressedThisFrame)
                HardDrop();

            if (kb.upArrowKey.wasPressedThisFrame    || kb.wKey.wasPressedThisFrame)
                TryRotate(1);

            if (kb.zKey.wasPressedThisFrame)
                TryRotate(-1);

            if (kb.spaceKey.wasPressedThisFrame)
                HardDrop();
        }

        HandlePointerInput();
    }

    private void HandlePointerInput()
    {
        // Leer estado crudo del puntero activo
        bool isPressed = false;
        Vector2 ptrPos = Vector2.zero;

        if (Mouse.current != null)
        {
            ptrPos = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.isPressed)
                isPressed = true;
        }
        
        if (!isPressed && Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.isPressed || touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                ptrPos = touch.position.ReadValue();
                isPressed = touch.press.isPressed;
            }
        }

        // Edge detection manual (fiable en todas las plataformas)
        bool pressStarted  = isPressed  && !_prevPointerPressed;
        bool pressEnded    = !isPressed && _prevPointerPressed;
        _prevPointerPressed = isPressed;

        // ── Inicio de gesto ──────────────────────────────────────────
        if (pressStarted)
        {
            Debug.Log($"[TetrisBoard] Drag INICIADO en la posición: {ptrPos}");
            _pointerStartPos   = ptrPos;
            _dragLastPos       = ptrPos;
            _isDraggingPointer = true;
        }

        // ── Drag activo ───────────────────────────────────────────────
        if (_isDraggingPointer && isPressed && !pressStarted)
        {
            float swipeThresh = Screen.width * 0.06f;
            float deltaX = ptrPos.x - _dragLastPos.x;

            if (Mathf.Abs(deltaX) >= swipeThresh)
            {
                int steps = Mathf.FloorToInt(Mathf.Abs(deltaX) / swipeThresh);
                int sign  = (int)Mathf.Sign(deltaX);

                for (int i = 0; i < steps; i++)
                    TryMove(new Vector2Int(sign, 0));

                _dragLastPos.x += steps * swipeThresh * sign;
            }

            float dropThresh  = Screen.height * 0.12f;
            float totalDeltaY = ptrPos.y - _pointerStartPos.y;

            if (totalDeltaY < -dropThresh)
            {
                float totalDeltaX = Mathf.Abs(ptrPos.x - _pointerStartPos.x);
                if (Mathf.Abs(totalDeltaY) > totalDeltaX * 1.5f)
                {
                    Debug.Log($"[TetrisBoard] HardDrop activado con swipe hacia abajo.");
                    HardDrop();
                    _isDraggingPointer = false;
                }
            }
        }

        // ── Fin de gesto ─────────────────────────────────────────────
        if (_isDraggingPointer && pressEnded)
        {
            Debug.Log($"[TetrisBoard] Drag FINALIZADO por soltar input.");
            _isDraggingPointer = false;

            float tapDistance = Screen.width * 0.04f;
            if (Vector2.Distance(_pointerStartPos, ptrPos) <= tapDistance)
            {
                Debug.Log($"[TetrisBoard] Tap detectado al finalizar drag - Rotando pieza.");
                TryRotate(1);
            }
        }

        // Si no hay input activo y quedó drag colgado, limpiar
        if (!isPressed)
            _isDraggingPointer = false;
    }

    // ════════════════════════════════════════════════════════════════
    //  CAÍDA AUTOMÁTICA
    // ════════════════════════════════════════════════════════════════

    private void HandleFall()
    {
        // 1. Validar continuamente el estado de Grounded (cubre deslizamientos y rotaciones)
        bool canMoveDown = IsValidPosition(_currentPiece.WithOffset(Vector2Int.down));
        
        if (canMoveDown)
        {
            _pieceGrounded = false;
        }
        else if (!_pieceGrounded)
        {
            _pieceGrounded = true;
            _lockTimer = 0f;
        }

        // 2. Caída por gravedad (intervalos regulares)
        _fallTimer += Time.deltaTime;
        if (_fallTimer >= fallInterval)
        {
            _fallTimer = 0f;
            if (canMoveDown)
            {
                TryMove(Vector2Int.down);
                
                // Re-evaluar si justo aterrizó tras caer
                if (!IsValidPosition(_currentPiece.WithOffset(Vector2Int.down)))
                {
                    _pieceGrounded = true;
                    _lockTimer = 0f;
                }
            }
        }

        // 3. Temporizador de fijación (Lock Delay) sobre suelo firme
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
        
        _pieceGrounded = true;
        _lockTimer = 0f;
        _fallTimer = 0f;
    }

    // ════════════════════════════════════════════════════════════════
    //  BLOQUEO Y SPAWN
    // ════════════════════════════════════════════════════════════════

    private void LockPiece()
    {
        // Fijar los colores de la pieza en la grilla permanente
        Sprite sprite = GetPieceSprite(_currentPiece.Type);
        foreach (var cell in _currentPiece.Cells)
        {
            if (IsInBounds(cell))
            {
                _grid[cell.x, cell.y]        = _renderers[cell.x, cell.y];
                _gridColors[cell.x, cell.y]  = Color.white;
                _gridSprites[cell.x, cell.y] = sprite;
            }
        }

        _pieceGrounded = false;
        _lockTimer     = 0f;
        _fallTimer     = 0f;

        // Limpiar la referencia antes de ClearLines para que RedrawGrid
        // no redibuje la pieza fijada sobre las líneas que deben eliminarse.
        _currentPiece = null;

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
            _grid[col, row]        = null;
            _gridColors[col, row]  = Color.clear;
            _gridSprites[col, row] = null;
        }
    }

    private void ShiftRowsDown(int clearedRow)
    {
        for (int row = clearedRow; row < rows - 1; row++)
            for (int col = 0; col < columns; col++)
            {
                _grid[col, row]        = _grid[col, row + 1];
                _gridColors[col, row]  = _gridColors[col, row + 1];
                _gridSprites[col, row] = _gridSprites[col, row + 1];
            }

        for (int col = 0; col < columns; col++)
        {
            _grid[col, rows - 1]        = null;
            _gridColors[col, rows - 1]  = Color.clear;
            _gridSprites[col, rows - 1] = null;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  DIBUJO
    // ════════════════════════════════════════════════════════════════

    private void DrawPiece(TetrisPiece piece)
    {
        Sprite sprite = GetPieceSprite(piece.Type);
        foreach (var cell in piece.Cells)
            if (IsInBounds(cell))
                SetRenderer(cell.x, cell.y, Color.white, true, sprite);
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
                    SetRenderer(col, row, _gridColors[col, row], true, _gridSprites[col, row]);

        // Dibujar pieza activa encima
        if (_currentPiece != null)
            DrawPiece(_currentPiece);
    }

    private void SetRenderer(int col, int row, Color color, bool visible, Sprite sprite = null)
    {
        var sr = _renderers[col, row];
        sr.enabled = visible;
        sr.color   = color;
        if (sprite != null)
            sr.sprite = sprite;
        else if (!visible)
            sr.sprite = null;
    }

    // ════════════════════════════════════════════════════════════════
    //  CONSTRUCCIÓN DEL POOL DE RENDERERS
    // ════════════════════════════════════════════════════════════════

    private void BuildRenderers()
    {
        if (_renderers != null) return;

        // Limpiar cualquier objeto huérfano para evitar piezas fantasmas
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        _renderers   = new SpriteRenderer[columns, rows];
        _grid        = new SpriteRenderer[columns, rows];
        _gridColors  = new Color[columns, rows];
        _gridSprites = new Sprite[columns, rows];

        // Origen: centrar el tablero en el GameObject
        float originX = -(columns * blockSize.x) / 2f + blockSize.x / 2f;
        float originY = -(rows    * blockSize.y) / 2f + blockSize.y / 2f;

        Sprite defaultSprite = (pieceSprites != null && pieceSprites.Length > 0) ? pieceSprites[0] : null;
        float spriteW = (defaultSprite != null) ? defaultSprite.bounds.size.x : 1f;
        float spriteH = (defaultSprite != null) ? defaultSprite.bounds.size.y : 1f;

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
                go.transform.localScale = new Vector3(
                    blockSize.x / spriteW,
                    blockSize.y / spriteH,
                    1f
                );

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite  = null;
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
        _score              = 0;
        OnScoreChanged?.Invoke(_score);
        _fallTimer          = 0f;
        _lockTimer          = 0f;
        _pieceGrounded      = false;
        _currentPiece       = null;
        _nextPiece          = null;
        _isDraggingPointer  = false;
        _prevPointerPressed = false;

        if (_renderers == null)
        {
            BuildRenderers();
        }

        for (int col = 0; col < columns; col++)
            for (int row = 0; row < rows; row++)
            {
                _grid[col, row]        = null;
                _gridColors[col, row]  = Color.clear;
                _gridSprites[col, row] = null;
                SetRenderer(col, row, Color.clear, false);
            }
    }

    private Sprite GetPieceSprite(TetrominoType type)
    {
        if (pieceSprites == null) return null;
        int index = (int)type;
        if (index < pieceSprites.Length && pieceSprites[index] != null)
            return pieceSprites[index];
        // Fallback al primer sprite disponible
        return pieceSprites.Length > 0 ? pieceSprites[0] : null;
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

        // localToWorldMatrix ya encapsula posición + rotación + escala (incluyendo parents).
        // Dibujamos en espacio LOCAL con las dimensiones locales puras: el motor hace el resto.
        float localW = columns * blockSize.x;
        float localH = rows    * blockSize.y;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(localW, localH, 0.01f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
