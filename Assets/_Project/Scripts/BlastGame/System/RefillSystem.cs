
using UnityEngine;

public class RefillSystem
{
    BoardModel board;

    public RefillSystem(BoardModel board)
    {
        this.board = board;
    }

    public void Refill()
    {
        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                if (board.GetTile(x, y) == null)
                {
                    TileColor tileColor = (TileColor)Random.Range(0, 4);
                    board.SetTile(x, y, new TileModel(x, y, tileColor));
                }
            }
        }
    }
}
