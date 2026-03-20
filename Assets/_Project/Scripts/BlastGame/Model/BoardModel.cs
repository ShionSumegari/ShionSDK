public class BoardModel
{
    public int width;
    public int height;
    
    public TileModel[,] grid;

    public BoardModel(int width, int height)
    {
        this.width = width;
        this.height = height;
        
        grid = new TileModel[width, height];
    }

    public TileModel GetTile(int x, int y)
    {
        if(x < 0 || x >= width || y < 0 || y >= height)
            return null;
        return grid[x, y];
    }

    public void SetTile(int x, int y, TileModel tile)
    {
        grid[x, y] = tile;
    }
}
