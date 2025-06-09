using UnityEngine;
using Sirenix.OdinInspector; 

//enum for type of unit 
public enum UnitType
{
    Regular,
    Elite,
    Boss,
    Null
}

[CreateAssetMenu(fileName = "UnitStats_New", menuName = "GameData/Unit Stats")]
public class UnitStats_SO : ScriptableObject
{
    [Title("Unit Combat Stats")]
    [MinValue(1)] // Assure que la vie est au moins 1
    public int Health = 100;

    [MinValue(0)] // La défense peut être 0
    public int Defense = 10;

    [MinValue(0)] // L'attaque peut être 0
    public int Attack = 15;

    [MinValue(1)] // Portée d'attaque minimale de 1 (ou 0 si tu permets des attaques sans portée?)
    public int AttackRange = 1; // En nombre de tuiles

    [MinValue(1)] // Délai minimum d'une pulsation/beat
    public int AttackDelay = 1; // En nombre de beats

    [Title("Unit Movement & Detection")]
    [MinValue(1)] // Délai minimum d'une pulsation/beat
    public int MovementDelay = 1; // En nombre de beats avant de bouger

    [MinValue(0)] // La détection peut être 0 (ne voit rien)
    public int DetectionRange = 3; // En nombre de tuiles
    
    [Title("Unit Type")]
    [EnumToggleButtons] // Permet de choisir le type de manière plus visuelle
    public UnitType Type = UnitType.Regular; // Type de l'unité, peut être Regular, Elite ou Boss
    
    
}