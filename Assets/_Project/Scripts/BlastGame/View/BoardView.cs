using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardView : MonoBehaviour
{
    public TileView tilePrefab;
    public TileView[,] tiles;
    
    private ObjectPooling<TileView> tilePool;

    public void Initialize(int width, int height)
    {
        tiles = new TileView[width, height];
        
        tilePool = new ObjectPooling<TileView>(
        createFunc:() => Instantiate(tilePrefab),
        onGet: tile => tile.gameObject.SetActive(true),
        onRelease: tile => tile.gameObject.SetActive(false)
        );

        tilePool.CreatePooling(width * height);
    }

    public void CreateTile(TileModel model, int spawnY)
    {
        TileView tile = tilePool.GetPooling();
        
        tiles[model.x, model.y] = tile;
        
        Vector2Int start = new Vector2Int(model.x, spawnY);
        Vector2Int target = new Vector2Int(model.x, model.y);
        
        tile.SetPosition(start.x, start.y);
        tile.SetColor(model.color);
        tile.FallTo(target);
    }
    
    public TileView CreateTile(TileModel model)
    {
        TileView tile = tilePool.GetPooling();

        tile.SetPosition(model.x,model.y);
        tile.SetColor(model.color);

        tiles[model.x,model.y] = tile;

        return tile;
    }

    public void RemoveTile(int x, int y)
    {
        if (tiles[x, y] != null)
        {
            tilePool.Release(tiles[x, y]);
            tiles[x, y] = null;
        }
    }

    public void MoveTile(int fromX,int fromY,int toX,int toY)
    {
        TileView tile = tiles[fromX, fromY];
        
        if(tile == null) return;
        tiles[fromX,fromY] = null;
        tiles[toX,toY] = tile;

        tile.FallTo(new Vector2(toX, toY));
    }
}
