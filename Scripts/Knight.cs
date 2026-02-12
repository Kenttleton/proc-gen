using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Knight : CharacterRig
{
	protected override Dictionary<State, string> StateToAnimation => new()
	{
		{ State.Idle, "Idle" },
		{ State.Walking, "Walking_B" },
		{ State.Running, "Running_A" },
		{ State.Attacking, "Attack" },
		{ State.Jumping, "Jump_Full_Short" },
		{ State.Blocking, "Blocking" },
		{ State.TakingDamage, "Hit_A" },
		{ State.Dying, "Death_B" },
		{ State.Dead, "Death_B_Pose" }
	};

	public override void _Ready()
	{
		base._Ready();
		// Load character loadout (for now, hardcoded to "Knight")
		var loadout = ResourceLoader.Load<CharacterLoadout>("res://Data/Characters/KnightLoadout.tres");
		if (loadout != null)
		{
			ApplyLoadout(loadout);
		}
		var proportions = ResourceLoader.Load<CharacterProportions>("res://Data/Characters/KnightProportions.tres");
		if (proportions != null)
		{
			MaxHeight = proportions.Height;
		}
	}
}
