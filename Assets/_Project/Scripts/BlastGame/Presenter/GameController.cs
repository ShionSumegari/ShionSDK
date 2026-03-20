using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController
{
    BoardModel board;
    BoardView view;
    
    MatchSystem match;
    GravitySystem gravity;
    RefillSystem refill;
    BoardChecker check;

    public GameController(BoardModel board, BoardView view)
    {
        this.board = board;
        this.view = view;

        match = new MatchSystem(board);
        gravity = new GravitySystem(board);
        refill = new RefillSystem(board);
        check = new BoardChecker(board, match);
    }

    public void Initalize()
    {
        BoardGenerator generator = new BoardGenerator(board);
        generator.Generate();
        
        view.Initialize(board.width, board.height);

        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                view.CreateTile(board.GetTile(x, y));
            }
        }
    }

    public void OnTileClicked(int x, int y)
    {
        var group = match.FindMatches(x, y);
        if(group.Count < GameConstance.MIN_TILE_GROUP) return;
        RemoveTiles(group);
        ResolveBoard();
    }

    void RemoveTiles(List<TileModel> tiles)
    {
        foreach (var tile in tiles)
        {
            board.SetTile(tile.x, tile.y, null);
            view.RemoveTile(tile.x, tile.y);
        }
    }

    void ResolveBoard()
    {
        var moves = gravity.Colapse();
        AnimateGravity(moves);
        refill.Refill();
        SyncSpawn();
        
        if(!check.HasMove())
            Initalize();
    }

    void AnimateGravity(List<TileMove> moves)
    {
        foreach (var move in moves)
        {
            view.MoveTile(move.fromX, move.fromY, move.toX, move.toY);
        }
    }

    void SyncSpawn()
    {
        for (int x = 0; x < board.width; x++)
        {
            int emptyCount = 0;
            for (int y = 0; y < board.height; y++)
            {
                if (view.tiles[x, y] != null) continue;
                
                TileModel tile = board.GetTile(x, y);
                if (tile == null) continue;
                
                int spawnY = board.height + emptyCount;
                view.CreateTile(tile, spawnY);
                emptyCount++;
            }
        }
    }
}
