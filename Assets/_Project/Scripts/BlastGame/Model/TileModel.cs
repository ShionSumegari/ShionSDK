public enum TileColor
{
    Green,
    Red,
    Blue,
    Yellow
}
public class TileModel
{
    public int x;
    public int y;
    public TileColor color;

    public TileModel(int x, int y, TileColor color)
    {
        this.x = x;
        this.y = y;
        this.color = color;
    }
}
