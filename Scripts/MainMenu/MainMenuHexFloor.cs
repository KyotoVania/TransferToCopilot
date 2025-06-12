using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;

public class MainMenuHexFloor : MonoBehaviour
{
    [Header("Hexagon Floor Settings")]
    [SerializeField] private GameObject hexagonTilePrefab;
    [SerializeField] private float hexSize = 1f;
    [Tooltip("Décalage Y pour positionner le sol à la bonne hauteur")]
    [SerializeField] private float yOffset = 0f;
    
    [Header("Grid Centering")]
    [SerializeField] private bool centerGrid = true;
    [Tooltip("Décale la grille pour qu'elle soit centrée sur ce GameObject")]
    [SerializeField] private bool useRectangularGrid = true;
    [ShowIf("useRectangularGrid")]
    [SerializeField] private int gridWidth = 10;
    [ShowIf("useRectangularGrid")]
    [SerializeField] private int gridHeight = 8;
    
    [Header("Spacing Settings")]
    [SerializeField] private float tileSpacing = 1.0f;
    [Tooltip("Espacement supplémentaire entre les tuiles (0 = tuiles collées)")]
    
    [HideIf("useRectangularGrid")]
    [SerializeField] private int circularRadius = 5;
    
    [Header("Visual Variety")]
    [SerializeField] private bool useRandomRotation = false;
    [ShowIf("useRandomRotation")]
    [SerializeField] private Vector3 randomRotationRange = new Vector3(0, 360, 0);
    
    [SerializeField] private bool useRandomScale = false;
    [ShowIf("useRandomScale")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.9f, 1.1f);
    
    [Header("Performance")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool useCoroutineGeneration = false;
    [ShowIf("useCoroutineGeneration")]
    [SerializeField] private int tilesPerFrame = 5;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private bool isGenerated = false;

    private void Start()
    {
        if (generateOnStart)
        {
            if (useCoroutineGeneration)
                StartCoroutine(GenerateFloorCoroutine());
            else
                GenerateFloor();
        }
    }

    [Button("Generate Floor", ButtonSizes.Large)]
    public void GenerateFloor()
    {
        if (hexagonTilePrefab == null)
        {
            Debug.LogError("[MainMenuHexFloor] Hexagon tile prefab is not assigned!");
            return;
        }

        ClearExistingFloor();

        if (useRectangularGrid)
            GenerateRectangularGrid();
        else
            GenerateCircularGrid();

        isGenerated = true;
        
        if (showDebugInfo)
            Debug.Log($"[MainMenuHexFloor] Floor generated with {transform.childCount} hexagon tiles");
    }

    [Button("Clear Floor", ButtonSizes.Medium)]
    public void ClearExistingFloor()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(transform.GetChild(i).gameObject);
            else
                DestroyImmediate(transform.GetChild(i).gameObject);
        }
        
        isGenerated = false;
    }

    private void GenerateRectangularGrid()
    {
        float hexWidth = hexSize * 1.5f + tileSpacing;
        float hexHeight = hexSize * Mathf.Sqrt(3f) * 0.5f + tileSpacing;
        
        Vector3 gridOffset = Vector3.zero;
        if (centerGrid)
        {
            float totalWidth = (gridWidth - 1) * hexWidth;
            float totalHeight = (gridHeight - 1) * hexHeight;
            gridOffset = new Vector3(-totalWidth * 0.5f, 0f, -totalHeight * 0.5f);
        }
        
        for (int row = 0; row < gridHeight; row++)
        {
            for (int col = 0; col < gridWidth; col++)
            {
                Vector3 position = CalculateHexPosition(col, row, hexSize, tileSpacing) + gridOffset + transform.position;
                CreateHexagonTile(position, col, row);
            }
        }
    }

    private void GenerateCircularGrid()
    {
        for (int q = -circularRadius; q <= circularRadius; q++)
        {
            int r1 = Mathf.Max(-circularRadius, -q - circularRadius);
            int r2 = Mathf.Min(circularRadius, -q + circularRadius);
            
            for (int r = r1; r <= r2; r++)
            {
                Vector3 position = AxialToWorldPosition(q, r, hexSize, tileSpacing) + transform.position;
                CreateHexagonTile(position, q, r);
            }
        }
    }

    private IEnumerator GenerateFloorCoroutine()
    {
        if (hexagonTilePrefab == null)
        {
            Debug.LogError("[MainMenuHexFloor] Hexagon tile prefab is not assigned!");
            yield break;
        }

        ClearExistingFloor();

        int tilesCreated = 0;
        
        float hexWidth = hexSize * 1.5f + tileSpacing;
        float hexHeight = hexSize * Mathf.Sqrt(3f) * 0.75f + tileSpacing;
        
        Vector3 gridOffset = Vector3.zero;
        if (centerGrid && useRectangularGrid)
        {
            float totalWidth = (gridWidth - 1) * hexWidth;
            float totalHeight = (gridHeight - 1) * hexHeight;
            gridOffset = new Vector3(-totalWidth * 0.5f, 0f, -totalHeight * 0.5f);
        }
        
        if (useRectangularGrid)
        {
            for (int row = 0; row < gridHeight; row++)
            {
                for (int col = 0; col < gridWidth; col++)
                {
                    Vector3 position = CalculateHexPosition(col, row, hexSize, tileSpacing) + gridOffset + transform.position;
                    CreateHexagonTile(position, col, row);
                    
                    tilesCreated++;
                    if (tilesCreated >= tilesPerFrame)
                    {
                        tilesCreated = 0;
                        yield return null;
                    }
                }
            }
        }
        else
        {
            for (int q = -circularRadius; q <= circularRadius; q++)
            {
                int r1 = Mathf.Max(-circularRadius, -q - circularRadius);
                int r2 = Mathf.Min(circularRadius, -q + circularRadius);
                
                for (int r = r1; r <= r2; r++)
                {
                    Vector3 position = AxialToWorldPosition(q, r, hexSize, tileSpacing) + transform.position;
                    CreateHexagonTile(position, q, r);
                    
                    tilesCreated++;
                    if (tilesCreated >= tilesPerFrame)
                    {
                        tilesCreated = 0;
                        yield return null;
                    }
                }
            }
        }

        isGenerated = true;
        
        if (showDebugInfo)
            Debug.Log($"[MainMenuHexFloor] Floor generated with {transform.childCount} hexagon tiles");
    }

    private Vector3 CalculateHexPosition(int col, int row, float size, float spacing)
    {
        float hexWidth = size * 1.5f + spacing;
        float hexHeight = size * Mathf.Sqrt(3f) * 0.75f + spacing;
        
        float xPos = col * hexWidth;
        float zPos = row * hexHeight;
        
        if (row % 2 == 1)
            xPos += hexWidth * 0.5f;
            
        return new Vector3(xPos, yOffset, zPos);
    }

    private Vector3 AxialToWorldPosition(int q, int r, float size, float spacing)
    {
        float hexWidth = size * 1.5f + spacing;
        float hexHeight = size * Mathf.Sqrt(3f) * 0.5f + spacing;
        
        float x = hexWidth * (2f/3f * q);
        float z = hexHeight * (Mathf.Sqrt(3f)/3f * q + 2f/3f * Mathf.Sqrt(3f) * r);
        
        return new Vector3(x, yOffset, z);
    }

    private void CreateHexagonTile(Vector3 position, int col, int row)
    {
        GameObject hexTile = Instantiate(hexagonTilePrefab, position, Quaternion.identity, transform);
        hexTile.name = $"HexTile_{col}_{row}";
        
        hexTile.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        hexTile.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        
        if (useRandomScale)
        {
            ApplyScaleVariation(hexTile);
        }
    }

    private void ApplyScaleVariation(GameObject hexTile)
    {
        if (useRandomScale)
        {
            float randomScale = Random.Range(scaleRange.x, scaleRange.y);
            hexTile.transform.localScale = Vector3.one * randomScale;
        }
    }

    private void ApplyVisualVariations(GameObject hexTile)
    {
        if (useRandomRotation)
        {
            Vector3 randomRot = new Vector3(
                Random.Range(-randomRotationRange.x, randomRotationRange.x),
                Random.Range(-randomRotationRange.y, randomRotationRange.y),
                Random.Range(-randomRotationRange.z, randomRotationRange.z)
            );
            hexTile.transform.localRotation = Quaternion.Euler(randomRot);
        }
        ApplyScaleVariation(hexTile);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugInfo) return;
        
        float totalHexSize = hexSize + tileSpacing;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWidth * totalHexSize * 1.5f, 0.1f, gridHeight * totalHexSize * Mathf.Sqrt(3f)));
    }
    
    public bool IsGenerated => isGenerated;
    public int TileCount => transform.childCount;
    
    public void RegenerateFloor()
    {
        if (isGenerated)
        {
            GenerateFloor();
        }
    }
}