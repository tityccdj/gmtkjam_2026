using System.Collections;
using UnityEngine;

public enum OrbType
{
    Red,
    Blue,
    Yellow,
    Green,
    Purple
}

public class Orb : MonoBehaviour
{
    public int column;
    public int row;
    public OrbType type;
    public Board board;
    
    private SpriteRenderer spriteRenderer;

    public void Init(int col, int r, OrbType orbType, Board b)
    {
        column = col;
        row = r;
        type = orbType;
        board = b;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateColor();
    }

    public void UpdateColor()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        
        switch (type)
        {
            case OrbType.Red:
                spriteRenderer.color = Color.red;
                break;
            case OrbType.Blue:
                spriteRenderer.color = Color.cyan;
                break;
            case OrbType.Yellow:
                spriteRenderer.color = Color.yellow;
                break;
            case OrbType.Green:
                spriteRenderer.color = Color.green;
                break;
            case OrbType.Purple:
                spriteRenderer.color = new Color(0.6f, 0.1f, 0.9f); // Purple
                break;
        }
    }

    private void OnMouseDown()
    {
        if (board.currentState == GameState.Wait) return;
        
        // เปลี่ยนสีให้เข้มขึ้นตอนกด (Tint)
        Color c = spriteRenderer.color;
        spriteRenderer.color = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 1f);
    }

    private void OnMouseUp()
    {
        // คืนค่าสีเดิมเมื่อปล่อยเมาส์
        UpdateColor();
        
        if (board.currentState == GameState.Wait) return;
        
        // กดเพื่อเลือกสลับตำแหน่ง
        board.SelectOrb(this);
    }
}
