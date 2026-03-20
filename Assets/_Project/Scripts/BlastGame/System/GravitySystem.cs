using System.Collections.Generic;

public class GravitySystem
{
    BoardModel board;

    public GravitySystem(BoardModel board)
    {
        this.board = board;
    }

    public List<TileMove> Colapse()
    {
        List<TileMove> moves = new List<TileMove>();
        for (int x = 0; x < board.width; x++)
        {
            int writeY = 0;
            for (int y = 0; y < board.height; y++)
            {
                TileModel tile = board.GetTile(x, y);
                if (tile != null)
                {
                    if (y != writeY)
                    {
                        moves.Add(new TileMove()
                        {
                            tile = tile,
                            fromX = x,
                            fromY = y,
                            toX = x,
                            toY = writeY
                        });
                        board.SetTile(x,writeY,tile);
                        board.SetTile(x,y,null);
                        
                        tile.x = x;
                        tile.y = writeY;
                    }
                    writeY++;
                }
            }
        }
        return moves;
    }
}

public struct TileMove
{
    public TileModel tile;
    public int fromX;
    public int fromY;
    public int toX;
    public int toY;
}
