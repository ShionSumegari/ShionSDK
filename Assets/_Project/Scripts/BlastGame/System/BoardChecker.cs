public class BoardChecker
{
    BoardModel board;
    MatchSystem match;

    public BoardChecker(BoardModel board, MatchSystem match)
    {
        this.board = board;
        this.match = match;
    }

    public bool HasMove()
    {
        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                var groupTile = match.FindMatches(x, y);
                if (groupTile.Count >= GameConstance.MIN_TILE_GROUP)
                    return true;
            }
        }
        return false;
    }
}
