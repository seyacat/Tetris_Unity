using UnityEngine;
using UnityEngine.UI;

public class NextPieceDisplay : MonoBehaviour
{
    [SerializeField] private TetrisBoard tetrisBoard;
    [SerializeField] private Image nextPieceImage;

    private void Update()
    {
        if (tetrisBoard == null || nextPieceImage == null) return;

        var nextPiece = tetrisBoard.NextPiece;
        if (nextPiece != null)
        {
            int index = (int)nextPiece.Type;
            if (index < tetrisBoard.pieceSprites.Length && tetrisBoard.pieceSprites[index] != null)
                nextPieceImage.sprite = tetrisBoard.pieceSprites[index];
            else if (tetrisBoard.pieceSprites.Length > 0)
                nextPieceImage.sprite = tetrisBoard.pieceSprites[0];
        }
        else
        {
            nextPieceImage.sprite = null;
        }
    }
}
