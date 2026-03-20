using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class MatchSystem
{
    BoardModel board;

    public MatchSystem(BoardModel board)
    {
        this.board = board;
    }

    public List<TileModel> FindMatches(int x, int y)
    {
        List<TileModel> result = new List<TileModel>();
        Queue<TileModel> queue = new Queue<TileModel>();
        
        TileModel start = board.GetTile(x, y);
        
        if(start == null) return result;
        
        bool[,] visited = new bool[board.width, board.height];
        queue.Enqueue(start);
        visited[x, y] = true;

        while (queue.Count > 0)
        {
            TileModel current = queue.Dequeue();
            
            result.Add(current);

            foreach (var n in GetNeighbor(current))
            {
                if (!visited[n.x, n.y] && n.color == start.color)
                {
                    visited[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }
        }

        return result;
    }

    private List<TileModel> GetNeighbor(TileModel tile)
    {
        List<TileModel> list = new List<TileModel>();

        int x = tile.x;
        int y = tile.y;
        
        TryAdd(list, x + 1, y);
        TryAdd(list, x - 1, y);
        TryAdd(list, x, y + 1);
        TryAdd(list, x, y - 1);
        
        return list;
    }

    void TryAdd(List<TileModel> list, int x, int y)
    {
        TileModel t = board.GetTile(x, y);
        
        if(t != null)
            list.Add(t);
    }
}
