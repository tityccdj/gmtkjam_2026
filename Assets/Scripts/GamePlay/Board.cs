using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState
{
    Wait,
    Move
}

public class Board : MonoBehaviour
{
    [Header("Board Settings")]
    public int width = 8;
    public int height = 8;
    public float offset = 1.0f;
    public float orbScale = 0.8f;
    
    [HideInInspector]
    public Orb[,] orbs;
    public GameState currentState = GameState.Move;
    public Orb selectedOrb;

    private Sprite defaultSprite;

    void Start()
    {
        defaultSprite = GetOrbSprite();
        orbs = new Orb[width, height];
        SetUp();
    }

    private Sprite GetOrbSprite()
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>("sprites/circle");
        if (sprites != null && sprites.Length > 0)
        {
            return sprites[0];
        }
        return Resources.GetBuiltinResource<Sprite>("ui/skin/knob.psd");
    }

    private void SetUp()
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                CreateOrbAt(i, j, true);
            }
        }
    }

    private void CreateOrbAt(int i, int j, bool checkMatches)
    {
        Vector2 tempPosition = new Vector2(i * offset, j * offset) + (Vector2)transform.position;
        GameObject orbObj = new GameObject($"Orb_{i}_{j}");
        orbObj.transform.position = tempPosition;
        orbObj.transform.parent = this.transform;
        orbObj.transform.localScale = new Vector3(orbScale, orbScale, 1f);
        
        SpriteRenderer sr = orbObj.AddComponent<SpriteRenderer>();
        sr.sprite = defaultSprite;
        
        // ใช้ BoxCollider2D ให้ขนาดพอดีกับช่อง เพื่อให้กดง่ายขึ้น (ไม่มีช่องโหว่ระหว่างลูกแก้ว)
        BoxCollider2D col = orbObj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(offset / orbScale, offset / orbScale);

        Orb orb = orbObj.AddComponent<Orb>();
        OrbType type;

        if (checkMatches)
        {
            type = GetRandomTypeWithoutMatch(i, j);
        }
        else
        {
            int maxTypes = System.Enum.GetValues(typeof(OrbType)).Length;
            type = (OrbType)Random.Range(0, maxTypes);
        }

        orb.Init(i, j, type, this);
        orbs[i, j] = orb;
    }

    private OrbType GetRandomTypeWithoutMatch(int column, int row)
    {
        OrbType type;
        int maxTypes = System.Enum.GetValues(typeof(OrbType)).Length;
        do
        {
            type = (OrbType)Random.Range(0, maxTypes);
        }
        while ((column >= 2 && orbs[column - 1, row] != null && orbs[column - 2, row] != null &&
                orbs[column - 1, row].type == type && orbs[column - 2, row].type == type) ||
               (row >= 2 && orbs[column, row - 1] != null && orbs[column, row - 2] != null &&
                orbs[column, row - 1].type == type && orbs[column, row - 2].type == type));
        return type;
    }



    public void SelectOrb(Orb orb)
    {
        if (selectedOrb == null)
        {
            selectedOrb = orb;
            selectedOrb.transform.localScale = new Vector3(orbScale * 1.2f, orbScale * 1.2f, 1f);
        }
        else if (selectedOrb == orb)
        {
            selectedOrb.transform.localScale = new Vector3(orbScale, orbScale, 1f);
            selectedOrb = null;
        }
        else
        {
            int colDiff = Mathf.Abs(selectedOrb.column - orb.column);
            int rowDiff = Mathf.Abs(selectedOrb.row - orb.row);
            
            if (colDiff + rowDiff == 1) // Adjacent
            {
                selectedOrb.transform.localScale = new Vector3(orbScale, orbScale, 1f);
                StartCoroutine(SwapCoroutine(selectedOrb, orb));
                selectedOrb = null;
            }
            else
            {
                selectedOrb.transform.localScale = new Vector3(orbScale, orbScale, 1f);
                selectedOrb = orb;
                selectedOrb.transform.localScale = new Vector3(orbScale * 1.2f, orbScale * 1.2f, 1f);
            }
        }
    }

    private IEnumerator SwapCoroutine(Orb first, Orb second)
    {
        if (selectedOrb != null)
        {
            selectedOrb.transform.localScale = new Vector3(orbScale, orbScale, 1f);
            selectedOrb = null;
        }
        
        currentState = GameState.Wait;
        
        // Swap data
        int tempCol = first.column;
        int tempRow = first.row;
        first.column = second.column;
        first.row = second.row;
        second.column = tempCol;
        second.row = tempRow;
        
        orbs[first.column, first.row] = first;
        orbs[second.column, second.row] = second;

        // Animate positions
        Vector2 firstPos = first.transform.position;
        Vector2 secondPos = second.transform.position;
        
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 5f;
            first.transform.position = Vector2.Lerp(firstPos, secondPos, t);
            second.transform.position = Vector2.Lerp(secondPos, firstPos, t);
            yield return null;
        }
        first.transform.position = secondPos;
        second.transform.position = firstPos;

        yield return new WaitForSeconds(0.1f);
        
        HashSet<Orb> matchedOrbs = FindMatches();
        
        if (matchedOrbs.Count > 0)
        {
            StartCoroutine(DestroyMatchesAndRefill(matchedOrbs));
        }
        else
        {
            // Revert swap data
            tempCol = first.column;
            tempRow = first.row;
            first.column = second.column;
            first.row = second.row;
            second.column = tempCol;
            second.row = tempRow;

            orbs[first.column, first.row] = first;
            orbs[second.column, second.row] = second;
            
            // Revert animation
            t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 5f;
                first.transform.position = Vector2.Lerp(secondPos, firstPos, t);
                second.transform.position = Vector2.Lerp(firstPos, secondPos, t);
                yield return null;
            }
            first.transform.position = firstPos;
            second.transform.position = secondPos;
            
            currentState = GameState.Move;
        }
    }

    private HashSet<Orb> FindMatches()
    {
        HashSet<Orb> matchedOrbs = new HashSet<Orb>();
        
        // Horizontal matches
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width - 2; i++)
            {
                if (orbs[i, j] != null && orbs[i + 1, j] != null && orbs[i + 2, j] != null)
                {
                    if (orbs[i, j].type == orbs[i + 1, j].type && orbs[i + 1, j].type == orbs[i + 2, j].type)
                    {
                        matchedOrbs.Add(orbs[i, j]);
                        matchedOrbs.Add(orbs[i + 1, j]);
                        matchedOrbs.Add(orbs[i + 2, j]);
                    }
                }
            }
        }

        // Vertical matches
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height - 2; j++)
            {
                if (orbs[i, j] != null && orbs[i, j + 1] != null && orbs[i, j + 2] != null)
                {
                    if (orbs[i, j].type == orbs[i, j + 1].type && orbs[i, j + 1].type == orbs[i, j + 2].type)
                    {
                        matchedOrbs.Add(orbs[i, j]);
                        matchedOrbs.Add(orbs[i, j + 1]);
                        matchedOrbs.Add(orbs[i, j + 2]);
                    }
                }
            }
        }
        
        return matchedOrbs;
    }

    private IEnumerator DestroyMatchesAndRefill(HashSet<Orb> matchedOrbs)
    {
        bool hasMatches = true;
        while (hasMatches)
        {
            // Destroy matches
            foreach (var orb in matchedOrbs)
            {
                if (orb != null)
                {
                    orbs[orb.column, orb.row] = null;
                    Destroy(orb.gameObject);
                }
            }
            yield return new WaitForSeconds(0.2f);

            // Collapse (Gravity)
            for (int i = 0; i < width; i++)
            {
                int nullCount = 0;
                for (int j = 0; j < height; j++)
                {
                    if (orbs[i, j] == null)
                    {
                        nullCount++;
                    }
                    else if (nullCount > 0)
                    {
                        orbs[i, j].row -= nullCount;
                        orbs[i, j - nullCount] = orbs[i, j];
                        orbs[i, j] = null;
                    }
                }
            }
            
            // Refill missing spots
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    if (orbs[i, j] == null)
                    {
                        CreateOrbAt(i, j, false);
                        
                        // Place them above the board to animate falling
                        Vector2 tempPosition = new Vector2(i * offset, height * offset) + (Vector2)transform.position;
                        orbs[i, j].transform.position = tempPosition;
                    }
                }
            }
            
            // Animate collapse and refill
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 5f;
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        if (orbs[i, j] != null)
                        {
                            Vector2 targetPos = new Vector2(i * offset, j * offset) + (Vector2)transform.position;
                            orbs[i, j].transform.position = Vector2.Lerp(orbs[i, j].transform.position, targetPos, t);
                        }
                    }
                }
                yield return null;
            }
            
            // Ensure exact final position
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    if (orbs[i, j] != null)
                    {
                        orbs[i, j].transform.position = new Vector2(i * offset, j * offset) + (Vector2)transform.position;
                    }
                }
            }

            // Check for chain reactions
            matchedOrbs = FindMatches();
            if (matchedOrbs.Count == 0)
            {
                hasMatches = false;
            }
        }
        
        currentState = GameState.Move;
    }
}
