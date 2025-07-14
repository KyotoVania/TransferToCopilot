using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines the state of a tile.
/// </summary>
public enum TileState { Default, Activated, Captured }
/// <summary>
/// Defines the type of a tile.
/// </summary>
public enum TileType { Ground, Water, Mountain }

/// <summary>
/// Represents a single tile in the hexagonal grid.
/// </summary>
public class Tile : MonoBehaviour
{
    [SerializeField] private int _column;
    [SerializeField] private int _row;
    [SerializeField] private TileState _tileState = TileState.Default;
    [SerializeField] private TileType _tileType = TileType.Ground;
    [SerializeField] private List<Tile> _neighbors = new List<Tile>();

    [SerializeField] private Building _building;
    [SerializeField] private Unit _unit;
    [SerializeField] private Environment _environment;

    /// <summary>
    /// Reference to the grid manager.
    /// </summary>
    protected HexGridManager gridManager;

    /// <summary>
    /// The column index of the tile in the grid.
    /// </summary>
    public int column { get => _column; set => _column = value; }
    /// <summary>
    /// The row index of the tile in the grid.
    /// </summary>
    public int row { get => _row; set => _row = value; }
    /// <summary>
    /// The current state of the tile.
    /// </summary>
    public TileState state
    {
        get => _tileState;
        protected set { if (_tileState != value) { _tileState = value; NotifyManagerOfStateChange(); } }
    }
    /// <summary>
    /// The type of the tile (e.g., Ground, Water).
    /// </summary>
    public TileType tileType
    {
        get => _tileType;
        set { if (_tileType != value) { _tileType = value; UpdateTileAppearance(); NotifyManagerOfStateChange(); } }
    }
    /// <summary>
    /// A list of the tile's neighbors.
    /// </summary>
    public List<Tile> Neighbors => _neighbors;
    /// <summary>
    /// The building currently on this tile, if any.
    /// </summary>
    public Building currentBuilding => _building;
    /// <summary>
    /// The unit currently on this tile, if any.
    /// </summary>
    public Unit currentUnit => _unit;
    /// <summary>
    /// The environment object on this tile, if any.
    /// </summary>
    public Environment currentEnvironment => _environment;

    /// <summary>
    /// Whether the tile is occupied by a building, unit, or blocking environment.
    /// </summary>
    public bool IsOccupied => _building != null || _unit != null ||
                              _tileType == TileType.Water ||
                              _tileType == TileType.Mountain ||
                              (_environment != null && _environment.IsBlocking);

    /// <summary>
    /// Whether the tile is currently reserved by the TileReservationController.
    /// </summary>
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

    /// <summary>
    /// Unity's Awake method.
    /// </summary>
    protected virtual void Awake()
    {
    }

    /// <summary>
    /// Unity's Start method.
    /// </summary>
    protected virtual void Start()
    {
        UpdateTileAppearance();
    }

    /// <summary>
    /// Unity's OnDestroy method.
    /// </summary>
    protected virtual void OnDestroy()
    {
    }

    /// <summary>
    /// Sets the grid manager reference.
    /// </summary>
    /// <param name="manager">The grid manager.</param>
    public void SetGridManager(HexGridManager manager) { gridManager = manager; }
    /// <summary>
    /// Sets the neighbors of this tile.
    /// </summary>
    /// <param name="newNeighbors">The list of neighboring tiles.</param>
    public void SetNeighbors(List<Tile> newNeighbors) { _neighbors = newNeighbors; }

    /// <summary>
    /// Assigns a unit to this tile.
    /// </summary>
    /// <param name="unit">The unit to assign.</param>
    public virtual void AssignUnit(Unit unit)
    {
        if (_tileType == TileType.Water || _tileType == TileType.Mountain) return;
        bool oldOccupiedState = IsOccupied;
        _unit = unit;
        if (oldOccupiedState != IsOccupied) NotifyManagerOfStateChange();
    }
    /// <summary>
    /// Removes the unit from this tile.
    /// </summary>
    public virtual void RemoveUnit()
    {
        if (_unit != null) { bool old = IsOccupied; _unit = null; if (old != IsOccupied) NotifyManagerOfStateChange(); }
    }
    /// <summary>
    /// Assigns a building to this tile.
    /// </summary>
    /// <param name="building">The building to assign.</param>
    public virtual void AssignBuilding(Building building)
    {
        if (_tileType == TileType.Water || _tileType == TileType.Mountain) return;
        bool oldOccupiedState = IsOccupied;
        _building = building;
        if (oldOccupiedState != IsOccupied) NotifyManagerOfStateChange();
    }
    /// <summary>
    /// Removes the building from this tile.
    /// </summary>
    public virtual void RemoveBuilding()
    {
        if (_building != null) { bool old = IsOccupied; _building = null; if (old != IsOccupied) NotifyManagerOfStateChange(); }
    }
    /// <summary>
    /// Assigns an environment object to this tile.
    /// </summary>
    /// <param name="environment">The environment object to assign.</param>
    public virtual void AssignEnvironment(Environment environment)
    {
        bool oldOccupiedState = IsOccupied;
        _environment = environment;
        if (environment != null && environment.transform.IsChildOf(transform))
        {
            Vector3 originalScale = environment.transform.localScale;
            Vector3 tileScale = transform.lossyScale;
            Vector3 counterScale = new Vector3(
                tileScale.x != 0 ? 1.0f / tileScale.x : 1.0f,
                tileScale.y != 0 ? 1.0f / tileScale.y : 1.0f,
                tileScale.z != 0 ? 1.0f / tileScale.z : 1.0f);
            environment.transform.localScale = Vector3.Scale(originalScale, counterScale);
        }
        if (oldOccupiedState != IsOccupied) NotifyManagerOfStateChange();
    }
    /// <summary>
    /// Removes the environment object from this tile.
    /// </summary>
    public virtual void RemoveEnvironment()
    {
        if (_environment != null) { bool old = IsOccupied; _environment = null; if (old != IsOccupied) NotifyManagerOfStateChange(); }
    }
    /// <summary>
    /// Notifies the grid manager that the state of this tile has changed.
    /// </summary>
    public void NotifyManagerOfStateChange() { gridManager?.NotifyTileChanged(this); }
    /// <summary>
    /// Updates the visual appearance of the tile.
    /// </summary>
    protected virtual void UpdateTileAppearance() { }
}
