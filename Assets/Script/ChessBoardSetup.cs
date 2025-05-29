using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class ChessBoardSetup : MonoBehaviour
{
    public class PiecePlacement
    {
        public string PieceName { get; set; }
        public Vector2Int Position { get; set; }
    }

    [Header("Board References")]
    [SerializeField] private Transform topLeftCorner;
    [SerializeField] private Transform bottomRightCorner;

    [SerializeField] private float xRotationOffset = 0f;
    
    [Header("Randomization Settings")]
    private float maxPositionOffset = 0.0f;
    private float maxRotationOffset = 360f;
    
    private Dictionary<string, GameObject> piecePrefabs = new Dictionary<string, GameObject>();
    private List<GameObject> activePieces = new List<GameObject>();
    private Dictionary<string, Queue<GameObject>> piecePool = new Dictionary<string, Queue<GameObject>>();
    
    private void Awake()
    {
        // Cache all piece prefabs from children
        foreach (Transform child in transform)
        {
            if (child != topLeftCorner && child != bottomRightCorner)
            {
                // Clean up the name by removing numbers and extra whitespace
                string cleanName = Regex.Replace(child.name, @"\d+", "").Trim();
                piecePrefabs[cleanName] = child.gameObject;
                child.name = cleanName;
                if (child.name != "Board") {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }
    
    public void SetupPosition(List<PiecePlacement> placements)
    {
        // Deactivate all active pieces and add them to the pool
        foreach (var piece in activePieces)
        {
            piece.SetActive(false);
            string poolKey = piece.name.Replace("(Clone)", "").Trim();
            if (!piecePool.ContainsKey(poolKey))
                piecePool[poolKey] = new Queue<GameObject>();
            piecePool[poolKey].Enqueue(piece);
        }
        activePieces.Clear();
        
        // Place pieces using pool
        foreach (var placement in placements)
        {
            if (piecePrefabs.TryGetValue(placement.PieceName, out GameObject piecePrefab))
            {
                GameObject piece = null;
                if (piecePool.ContainsKey(placement.PieceName) && piecePool[placement.PieceName].Count > 0)
                {
                    piece = piecePool[placement.PieceName].Dequeue();
                }
                else
                {
                    piece = Instantiate(piecePrefab, transform);
                }
                piece.SetActive(true);
                
                // Calculate position
                Vector3 boardSize = bottomRightCorner.position - topLeftCorner.position;
                Vector3 squareSize = new Vector3(boardSize.x / 8f, 0, boardSize.z / 8f);
                
                Vector3 basePosition = topLeftCorner.position + 
                    new Vector3(squareSize.x * (placement.Position.x + 0.5f), 0, squareSize.z * (placement.Position.y + 0.5f));
                
                // Add random offset
                Vector3 randomOffset = new Vector3(
                    Random.Range(-maxPositionOffset, maxPositionOffset),
                    0,
                    Random.Range(-maxPositionOffset, maxPositionOffset)
                );
                
                piece.transform.position = basePosition + randomOffset;
                
                // Add random rotation
                float randomRotation = Random.Range(-maxRotationOffset, maxRotationOffset);
                Quaternion rotation = Quaternion.Euler(0, randomRotation, 0);
                if (xRotationOffset != 0) {
                    rotation = Quaternion.Euler(xRotationOffset, 0, randomRotation);
                }
                piece.transform.rotation = rotation;
                
                activePieces.Add(piece);
            }
        }
    }

    // Deactivate all pieces (for when board is hidden)
    public void DeactivateAllPieces()
    {
        foreach (var piece in activePieces)
        {
            piece.SetActive(false);
            string poolKey = piece.name.Replace("(Clone)", "").Trim();
            if (!piecePool.ContainsKey(poolKey))
                piecePool[poolKey] = new Queue<GameObject>();
            piecePool[poolKey].Enqueue(piece);
        }
        activePieces.Clear();
    }

    public List<GameObject> GetActivePieces()
    {
        return activePieces;
    }

    public float GetSquareWidth()
    {
        Vector3 boardSize = bottomRightCorner.position - topLeftCorner.position;
        return boardSize.x / 8f;
    }
} 