
using UnityEngine;

public class BoardGenerator
{
    BoardModel board;

    public BoardGenerator(BoardModel board)
    {
        this.board = board;
    }

    public void Generate()
    {
        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                TileColor color = (TileColor)Random.Range(0, 4);

                board.SetTile(x, y, new TileModel(x, y, color));
            }
        }
    }
}
