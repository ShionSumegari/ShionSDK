using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileView : MonoBehaviour
{
    public int x;
    public int y;
    
    public SpriteRenderer sprite;

    public void SetPosition(int x, int y)
    {
        this.x = x;
        this.y = y;
        
        transform.position = new Vector2(x, y);
    }

    public void SetColor(TileColor color)
    {
        switch (color)
        {
            case TileColor.Blue: sprite.color = Color.blue; break;
            case TileColor.Red: sprite.color = Color.red; break;
            case TileColor.Yellow: sprite.color = Color.yellow; break;
            case TileColor.Green: sprite.color = Color.green; break;
        }
    }
    
    public void FallTo(Vector2 target)
    {
        StartCoroutine(Fall(target));
    }

    IEnumerator Fall(Vector2 target)
    {
        Vector3 start = transform.position;
        float t = 0;

        while(t < 1)
        {
            t += Time.deltaTime * 6f;
            transform.position = Vector2.Lerp(start,target,t);
            yield return null;
        }

        transform.position = target;
    }
}
