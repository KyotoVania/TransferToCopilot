using UnityEngine;
using System.Collections.Generic;

// Les Enums restent les mêmes
public enum TileState
{
    Default,
    Activated,
    Captured
}

public enum TileType
{
    Ground,
    Water,
    Mountain
}

public class Tile : MonoBehaviour
{
    // --- Champs Serialisés pour l'Inspecteur ---
    [SerializeField] private int _column;
    [SerializeField] private int _row;
    [SerializeField] private TileState _tileState = TileState.Default;
    [SerializeField] private TileType _tileType = TileType.Ground;
    [SerializeField] private List<Tile> _neighbors = new List<Tile>(); // Généralement rempli par HexGridManager

    // --- Références aux objets sur la tuile ---
    // Ces champs sont 'protected set' pour que les classes dérivées puissent y écrire
    // mais ils sont principalement gérés par AssignUnit/RemoveUnit, AssignBuilding/RemoveBuilding, etc.
    [SerializeField] private Building _building;
    [SerializeField] private Unit _unit;
    [SerializeField] private Environment _environment;

    // --- Références Internes ---
    protected HexGridManager gridManager; // Référence au manager de la grille
    protected Vector3 originalWorldPosition; // Position initiale de la tuile

    // --- Propriétés Publiques ---
    public int column
    {
        get => _column;
        set => _column = value; // Principalement pour initialisation par HexGridManager
    }

    public int row
    {
        get => _row;
        set => _row = value; // Principalement pour initialisation par HexGridManager
    }

    public TileState state
    {
        get => _tileState;
        protected set
        {
            if (_tileState != value)
            {
                _tileState = value;
                NotifyManagerOfStateChange(); // Notifier le manager
            }
        }
    }

    public TileType tileType
    {
        get => _tileType;
        set
        {
            if (_tileType != value)
            {
                _tileType = value;
                UpdateTileAppearance();
                NotifyManagerOfStateChange(); // Notifier le manager
            }
        }
    }

    public List<Tile> Neighbors => _neighbors;

    public Building currentBuilding => _building;
    public Unit currentUnit => _unit;
    public Environment currentEnvironment => _environment;

    // La tuile est considérée comme occupée si un bâtiment ou une unité est dessus,
    // ou si c'est un type de tuile bloquant (eau, montagne),
    // ou si un environnement bloquant est dessus.
    public bool IsOccupied => _building != null || _unit != null ||
                              _tileType == TileType.Water ||
                              _tileType == TileType.Mountain ||
                              (_environment != null && _environment.IsBlocking);

    // IsReserved interroge maintenant le TileReservationController
    public bool IsReserved
    {
        get
        {
            if (TileReservationController.Instance != null)
            {
                return TileReservationController.Instance.IsTileReserved(new Vector2Int(_column, _row));
            }
            // Si le contrôleur n'existe pas, on pourrait considérer qu'aucune tuile n'est réservée
            // ou logger un warning. Pour l'instant, on retourne false.
            // Debug.LogWarning("[Tile] TileReservationController.Instance is null. Cannot check reservation status.");
            return false;
        }
    }

    // --- Initialisation et Cycle de Vie ---
    protected virtual void Start()
    {
        originalWorldPosition = transform.position;
        UpdateTileAppearance();
        // HexGridManager s'occupera d'appeler SetGridManager et SetupNeighbors
    }

    protected virtual void OnDestroy()
    {
        // Nettoyer les références si nécessaire
        gridManager = null; // Évite les références potentielles après destruction
        // Les unités/bâtiments devraient gérer leur propre nettoyage et se retirer de la tuile.
    }

    public void SetGridManager(HexGridManager manager)
    {
        gridManager = manager;
    }

    public void SetNeighbors(List<Tile> newNeighbors)
    {
        _neighbors = newNeighbors;
    }

    // --- Gestion des Occupants ---
    public virtual void AssignUnit(Unit unit)
    {
        if (_tileType == TileType.Water || _tileType == TileType.Mountain)
        {
            Debug.LogWarning($"[{name}] Cannot assign unit '{unit?.name}' to a {_tileType} tile.");
            return;
        }
        bool oldOccupiedState = IsOccupied;
        _unit = unit;
        if (oldOccupiedState != IsOccupied)
        {
            NotifyManagerOfStateChange();
        }
    }

    public virtual void RemoveUnit()
    {
        if (_unit != null)
        {
            bool oldOccupiedState = IsOccupied;
            _unit = null;
            if (oldOccupiedState != IsOccupied)
            {
                NotifyManagerOfStateChange();
            }
        }
    }

    public virtual void AssignBuilding(Building building)
    {
        if (_tileType == TileType.Water || _tileType == TileType.Mountain)
        {
            Debug.LogWarning($"[{name}] Cannot assign building '{building?.name}' to a {_tileType} tile.");
            return;
        }
        bool oldOccupiedState = IsOccupied;
        _building = building;
        if (oldOccupiedState != IsOccupied)
        {
            NotifyManagerOfStateChange();
        }
    }

    public virtual void RemoveBuilding()
    {
        if (_building != null)
        {
            bool oldOccupiedState = IsOccupied;
            _building = null;
            if (oldOccupiedState != IsOccupied)
            {
                NotifyManagerOfStateChange();
            }
        }
    }

    public virtual void AssignEnvironment(Environment environment)
    {
        bool oldOccupiedState = IsOccupied; // Sauvegarder l'état avant changement
        _environment = environment;
        // La logique de mise à l'échelle de l'environnement si enfant est bonne à garder si vous la faites ainsi.
        if (environment != null && environment.transform.IsChildOf(transform))
        {
            Vector3 originalScale = environment.transform.localScale;
            Vector3 tileScale = transform.lossyScale;
            Vector3 counterScale = new Vector3(
                tileScale.x != 0 ? 1.0f / tileScale.x : 1.0f,
                tileScale.y != 0 ? 1.0f / tileScale.y : 1.0f,
                tileScale.z != 0 ? 1.0f / tileScale.z : 1.0f
            );
            environment.transform.localScale = Vector3.Scale(originalScale, counterScale);
        }
        if (oldOccupiedState != IsOccupied) // Si l'état d'occupation a changé
        {
            NotifyManagerOfStateChange();
        }
    }

    public virtual void RemoveEnvironment()
    {
        bool oldOccupiedState = IsOccupied;
        _environment = null;
        if (oldOccupiedState != IsOccupied)
        {
            NotifyManagerOfStateChange();
        }
    }

    public void NotifyManagerOfStateChange()
    {
        if (gridManager != null) // gridManager est accessible ici car nous sommes dans la classe Tile
        {
            gridManager.NotifyTileChanged(this);
        }
    }

    // --- Apparence ---
    protected virtual void UpdateTileAppearance()
    {
        // Logique pour changer l'apparence de la tuile en fonction de son type ou état.
        // Exemple :
        // MeshRenderer renderer = GetComponent<MeshRenderer>();
        // if (renderer != null)
        // {
        //     Material mat = renderer.material;
        //     switch (_tileType)
        //     {
        //         case TileType.Water: mat.color = Color.blue; break;
        //         case TileType.Mountain: mat.color = Color.grey; break;
        //         default: mat.color = Color.white; break;
        //     }
        // }
    }
}