using System;
using Unity.Behavior;

[BlackboardEnum]
public enum AIActionType
{
    None,
	MoveToBuilding,
	MoveToUnit,
	AttackUnit,
	AttackBuilding,
	CaptureBuilding,
	CheerAndDespawn,
	DefendPosition 
}
