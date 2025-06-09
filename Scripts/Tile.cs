// Fichier: Scripts2/Tile.cs
using UnityEngine;
using System.Collections.Generic;

public enum TileState { Default, Activated, Captured } // Assurez-vous que ces enums sont définis
public enum TileType { Ground, Water, Mountain } // Assurez-vous que ces enums sont définis

public class Tile : MonoBehaviour
{
    [SerializeField] private int _column;
    [SerializeField] private int _row;
    [SerializeField] private TileState _tileState = TileState.Default;
    [SerializeField] private TileType _tileType = TileType.Ground; // Gardez le type par défaut
    [SerializeField] private List<Tile> _neighbors = new List<Tile>();

    // Références aux objets sur la tuile
    [SerializeField] private Building _building;
    [SerializeField] private Unit _unit;
    [SerializeField] private Environment _environment;

    protected HexGridManager gridManager;
    // private Vector3 originalWorldPosition; // SUPPRIMÉ
    // private bool isBasePositionCaptured = false; // SUPPRIMÉ

    public int column { get => _column; set => _column = value; }
    public int row { get => _row; set => _row = value; }
    public TileState state
    {
        get => _tileState;
        protected set { if (_tileState != value) { _tileState = value; NotifyManagerOfStateChange(); } }
    }
    public TileType tileType
    {
        get => _tileType;
        set { if (_tileType != value) { _tileType = value; UpdateTileAppearance(); NotifyManagerOfStateChange(); } }
    }
    public List<Tile> Neighbors => _neighbors;
    public Building currentBuilding => _building;
    public Unit currentUnit => _unit;
    public Environment currentEnvironment => _environment;

    public bool IsOccupied => _building != null || _unit != null ||
                              _tileType == TileType.Water ||
                              _tileType == TileType.Mountain ||
                              (_environment != null && _environment.IsBlocking);

    public bool IsReserved
    {
        get
        {
            if (TileReservationController.Instance != null)
            {
                return TileReservationController.Instance.IsTileReserved(new Vector2Int(_column, _row));
            }
            return false;
        }
    }

    // Awake est maintenant plus simple
    protected virtual void Awake()
    {
        // Plus besoin de capturer originalWorldPosition ici
    }

    // Start est maintenant plus simple
    protected virtual void Start()
    {
        UpdateTileAppearance();
    }

    protected virtual void OnDestroy()
    {
        // Si HexGridManager est un singleton qui persiste, pas besoin de nullifier la référence ici,
        // sauf si vous avez une logique spécifique de nettoyage de scène.
        // gridManager = null;
    }

    public void SetGridManager(HexGridManager manager) { gridManager = manager; }
    public void SetNeighbors(List<Tile> newNeighbors) { _neighbors = newNeighbors; }

    public virtual void AssignUnit(Unit unit)
    {
        if (_tileType == TileType.Water || _tileType == TileType.Mountain) return;
        bool oldOccupiedState = IsOccupied;
        _unit = unit;
        if (oldOccupiedState != IsOccupied) NotifyManagerOfStateChange();
    }
    public virtual void RemoveUnit()
    {
        if (_unit != null) { bool old = IsOccupied; _unit = null; if (old != IsOccupied) NotifyManagerOfStateChange(); }
    }
    public virtual void AssignBuilding(Building building)
    {
        if (_tileType == TileType.Water || _tileType == TileType.Mountain) return;
        bool oldOccupiedState = IsOccupied;
        _building = building;
        if (oldOccupiedState != IsOccupied) NotifyManagerOfStateChange();
    }
    public virtual void RemoveBuilding()
    {
        if (_building != null) { bool old = IsOccupied; _building = null; if (old != IsOccupied) NotifyManagerOfStateChange(); }
    }
    public virtual void AssignEnvironment(Environment environment)
    {
        bool oldOccupiedState = IsOccupied;
        _environment = environment;
        if (environment != null && environment.transform.IsChildOf(transform))
        {
            // La logique de compensation d'échelle peut rester si nécessaire
            Vector3 originalScale = environment.transform.localScale;
            Vector3 tileScale = transform.lossyScale; // Utiliser lossyScale si la tuile elle-même est scalée
            Vector3 counterScale = new Vector3(
                tileScale.x != 0 ? 1.0f / tileScale.x : 1.0f,
                tileScale.y != 0 ? 1.0f / tileScale.y : 1.0f,
                tileScale.z != 0 ? 1.0f / tileScale.z : 1.0f);
            environment.transform.localScale = Vector3.Scale(originalScale, counterScale);
        }
        if (oldOccupiedState != IsOccupied) NotifyManagerOfStateChange();
    }
    public virtual void RemoveEnvironment()
    {
        if (_environment != null) { bool old = IsOccupied; _environment = null; if (old != IsOccupied) NotifyManagerOfStateChange(); }
    }
    public void NotifyManagerOfStateChange() { gridManager?.NotifyTileChanged(this); }
    protected virtual void UpdateTileAppearance() { /* Logique visuelle ici, ne doit pas dépendre de originalWorldPosition */ }
}