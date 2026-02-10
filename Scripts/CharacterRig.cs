using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CharacterRig : Node3D
{
    [Signal] public delegate void AnimationEventEventHandler(string eventName);
    [Signal] public delegate void AttackHitEventHandler();
    [Signal] public delegate void FootstepEventHandler();
    [Signal] public delegate void DeathEventHandler();

    [ExportGroup("Debug")]
    [Export] public bool EnableDebugOutput = false;

    [ExportGroup("Animation")]
    [Export] public NodePath AnimationPlayerPath;
    [Export] public NodePath AnimationTreePath;
    [Export] public float AnimationBlendTime = 0.2f;
    [Export] public float StateChangeCooldown = 0.1f;

    [ExportGroup("Skeleton")]
    [Export] public NodePath SkeletonPath = "Rig/Skeleton3D";

    [ExportGroup("Loadout")]
    [Export] public CharacterLoadout DefaultLoadout;

    [ExportGroup("Equipment Slots (BoneAttachment3D)")]
    [Export] public NodePath HeadSlotPath;
    [Export] public NodePath LeftHandSlotPath;
    [Export] public NodePath RightHandSlotPath;
    [Export] public NodePath TorsoSlotPath;

    [ExportGroup("Collision")]
    [Export] public NodePath BodyColliderPath;
    [Export] public NodePath HurtboxesRootPath;
    [Export] public NodePath WeaponHitboxPath;
    protected CollisionShape3D BodyCollider;
    protected Node3D HurtboxesRoot;
    protected readonly List<CollisionShape3D> Hurtboxes = new();
    protected Area3D WeaponHitbox;


    protected AnimationPlayer AnimPlayer;
    protected AnimationTree AnimTree;
    protected AnimationNodeStateMachinePlayback StateMachine;
    private State _currentState = State.Idle;
    private double _lastStateChangeTime = 0;
    protected Skeleton3D Skeleton;

    public enum EquipmentSlot
    {
        Head,
        LeftHand,
        RightHand,
        Torso
    }

    protected readonly Dictionary<EquipmentSlot, List<MeshInstance3D>> Equipment = new();

    public enum State
    {
        Idle,
        PickingUp,
        Interacting,
        UsingItem,
        Walking,
        Running,
        Jumping,
        DodgingLeft,
        DodgingRight,
        Block,
        Blocking,
        Attacking,
        TakingDamage,
        Dying,
        Dead
    }

    protected virtual Dictionary<State, string> StateToAnimation => new();

    public IReadOnlyList<string> AnimationList { get; private set; }

    public override void _Ready()
    {
        CacheNodes();
        CacheEquipment();
        CacheAnimations();
        InitializeStateMachine();
        HideAllEquipment();
        ApplyLoadout(DefaultLoadout);
        AutoSizeMovementCapsule();

    }

    protected static List<T> GetChildrenRecursive<T>(Node node) where T : Node
    {
        var result = new List<T>();
        foreach (var child in node.GetChildren())
        {
            if (child is T match)
                result.Add(match);

            result.AddRange(GetChildrenRecursive<T>(child));
        }
        return result;
    }


    protected virtual void AutoSizeMovementCapsule()
    {
        if (BodyColliderPath == null)
        {
            GD.PushWarning("CharacterRig: BodyCollider is missing.");
            return;
        }

        BodyCollider = GetNodeOrNull<CollisionShape3D>(BodyColliderPath);
        if (BodyCollider == null)
        {
            GD.PushWarning("CharacterRig: BodyCollider node not found.");
            return;
        }

        var capsule = BodyCollider.Shape as CapsuleShape3D;
        if (capsule == null)
        {
            GD.PushWarning("CharacterRig: BodyCollider shape is not a CapsuleShape3D.");
            capsule = new CapsuleShape3D();
        }

        // Find all mesh instances in the entire character tree
        var meshes = GetChildrenRecursive<MeshInstance3D>(this);

        if (meshes.Count == 0)
        {
            GD.PushWarning("CharacterRig: No meshes found for capsule sizing.");
            return;
        }

        // Filter to body part meshes
        var bodyMeshes = new List<MeshInstance3D>();
        foreach (var mesh in meshes)
        {
            string name = mesh.Name.ToString().ToLower();
            if (name.Contains("head") || name.Contains("body") ||
                name.Contains("leg") || name.Contains("arm") ||
                name.Contains("torso") || name.Contains("chest") ||
                name.Contains("spine") || name.Contains("pelvis"))
            {
                bodyMeshes.Add(mesh);
                if (EnableDebugOutput) GD.Print($"CharacterRig: Included mesh '{mesh.Name}' for capsule sizing.");
            }
        }

        if (bodyMeshes.Count == 0)
        {
            GD.PushWarning("CharacterRig: No body meshes found after filtering.");
            return;
        }

        // KEY FIX: Get combined AABB in the COLLIDER'S parent space
        // This way it works regardless of where the collider is in the hierarchy
        Node3D colliderParent = BodyCollider.GetParent<Node3D>();
        if (colliderParent == null)
        {
            GD.PushWarning("CharacterRig: CollisionShape3D has no Node3D parent.");
            return;
        }

        Aabb? combinedAabb = null;

        foreach (var mesh in bodyMeshes)
        {
            // Get mesh AABB in its own local space
            Aabb localAabb = mesh.GetAabb();

            // Transform to global space, then to collider parent's space
            Transform3D meshToGlobal = mesh.GlobalTransform;
            Transform3D globalToColliderParent = colliderParent.GlobalTransform.AffineInverse();
            Transform3D meshToColliderParent = globalToColliderParent * meshToGlobal;

            Aabb transformedAabb = TransformAabb(localAabb, meshToColliderParent);

            if (combinedAabb == null)
            {
                combinedAabb = transformedAabb;
            }
            else
            {
                combinedAabb = combinedAabb.Value.Merge(transformedAabb);
            }
        }

        if (combinedAabb == null)
        {
            GD.PushWarning("CharacterRig: Failed to calculate combined AABB.");
            return;
        }

        Aabb aabb = combinedAabb.Value;

        // Calculate capsule dimensions
        float fullHeight = aabb.Size.Y;

        // Radius from center
        Vector3 center = aabb.GetCenter();
        float maxRadiusX = Mathf.Max(
            Mathf.Abs(aabb.Position.X - center.X),
            Mathf.Abs(aabb.End.X - center.X)
        );
        float maxRadiusZ = Mathf.Max(
            Mathf.Abs(aabb.Position.Z - center.Z),
            Mathf.Abs(aabb.End.Z - center.Z)
        );
        float radius = Mathf.Max(maxRadiusX, maxRadiusZ);

        // Add padding
        radius *= 1.05f;

        // Capsule cylinder height
        float cylinderHeight = fullHeight - (2.0f * radius);

        // Minimum sizes
        cylinderHeight = Mathf.Max(0.1f, cylinderHeight);
        radius = Mathf.Max(0.1f, radius);

        // Apply to capsule
        capsule.Height = cylinderHeight;
        capsule.Radius = radius;
        BodyCollider.Shape = capsule;

        // Position capsule in its parent's local space
        float capsuleBottom = aabb.Position.Y;
        float capsuleCenterY = capsuleBottom + (fullHeight * 0.5f);

        BodyCollider.Position = new Vector3(
            center.X,
            capsuleCenterY,
            center.Z
        );

        if (EnableDebugOutput)
        {
            // Debug output
            GD.Print($"=== Capsule Auto-Size Debug ===");
            GD.Print($"Collider parent: {colliderParent.Name}");
            GD.Print($"Capsule: Height={capsule.Height:F2}, Radius={radius:F2}");
            GD.Print($"Capsule local position: {BodyCollider.Position}");
            GD.Print($"Character AABB in collider space: Min={aabb.Position}, Max={aabb.End}, Size={aabb.Size}");
            GD.Print($"Processed {bodyMeshes.Count} body meshes");
        }
    }

    private Aabb TransformAabb(Aabb aabb, Transform3D transform)
    {
        // Get all 8 corners of the AABB
        Vector3[] corners = new Vector3[8]
        {
        aabb.Position,
        aabb.Position + new Vector3(aabb.Size.X, 0, 0),
        aabb.Position + new Vector3(0, aabb.Size.Y, 0),
        aabb.Position + new Vector3(0, 0, aabb.Size.Z),
        aabb.Position + new Vector3(aabb.Size.X, aabb.Size.Y, 0),
        aabb.Position + new Vector3(aabb.Size.X, 0, aabb.Size.Z),
        aabb.Position + new Vector3(0, aabb.Size.Y, aabb.Size.Z),
        aabb.Position + aabb.Size
        };

        // Transform all corners
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = transform * corners[i];
        }

        // Find min/max of transformed corners
        Vector3 min = corners[0];
        Vector3 max = corners[0];

        for (int i = 1; i < corners.Length; i++)
        {
            min = new Vector3(
                Mathf.Min(min.X, corners[i].X),
                Mathf.Min(min.Y, corners[i].Y),
                Mathf.Min(min.Z, corners[i].Z)
            );
            max = new Vector3(
                Mathf.Max(max.X, corners[i].X),
                Mathf.Max(max.Y, corners[i].Y),
                Mathf.Max(max.Z, corners[i].Z)
            );
        }

        return new Aabb(min, max - min);
    }

    protected virtual void GenerateHurtboxesFromMeshes()
    {
        HurtboxesRoot = GetNodeOrNull<Node3D>(HurtboxesRootPath);
        if (HurtboxesRoot == null)
        {
            GD.PushWarning("CharacterRig: Hurtboxes root not found.");
            return;
        }

        Hurtboxes.Clear();

        // Gather all body meshes (ignore weapons/clothes)
        var meshes = GetChildrenRecursive<MeshInstance3D>(this)
            .Where(m => !m.Name.ToString().Contains("Weapon") && !m.Name.ToString().Contains("Cape"));

        foreach (var mesh in meshes)
        {
            if (mesh.Mesh == null)
                continue;

            // Create an Area3D for detection
            var area = new Area3D();
            area.Name = $"{mesh.Name}_Hurtbox";
            HurtboxesRoot.AddChild(area);

            // Use convex hull collider from mesh
            var collision = new CollisionShape3D();
            collision.Shape = mesh.Mesh.CreateConvexShape();
            area.AddChild(collision);

            // Align collider to mesh
            area.GlobalTransform = mesh.GlobalTransform;

            Hurtboxes.Add(collision);
        }
    }

    protected void CacheWeaponHitbox()
    {
        WeaponHitbox = GetNodeOrNull<Area3D>(WeaponHitboxPath);
        if (WeaponHitbox != null)
            WeaponHitbox.Monitoring = false; // off by default
    }

    protected virtual void CacheNodes()
    {
        AnimPlayer = GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath)
            ?? GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

        AnimTree = GetNodeOrNull<AnimationTree>(AnimationTreePath)
            ?? GetNodeOrNull<AnimationTree>("AnimationTree");

        Skeleton = GetNodeOrNull<Skeleton3D>(SkeletonPath);

        if (AnimPlayer == null)
            GD.PushError("CharacterRig: AnimationPlayer not found.");

        if (AnimTree != null)
            AnimTree.Active = true;
    }

    protected virtual void CacheEquipment()
    {
        Equipment[EquipmentSlot.Head] = GetSlotMeshes(HeadSlotPath, "Head");
        Equipment[EquipmentSlot.LeftHand] = GetSlotMeshes(LeftHandSlotPath, "LeftHand");
        Equipment[EquipmentSlot.RightHand] = GetSlotMeshes(RightHandSlotPath, "RightHand");
        Equipment[EquipmentSlot.Torso] = GetSlotMeshes(TorsoSlotPath, "Torso");
    }

    protected List<MeshInstance3D> GetSlotMeshes(NodePath path, string slotName)
    {
        if (path == null || path.IsEmpty)
        {
            GD.PushWarning($"CharacterRig: {slotName} slot path not set.");
            return new List<MeshInstance3D>();
        }

        var attachment = GetNodeOrNull<BoneAttachment3D>(path);
        if (attachment == null)
        {
            GD.PushWarning($"CharacterRig: BoneAttachment3D not found at path {path}.");
            return new List<MeshInstance3D>();
        }

        var meshes = attachment.GetChildren()
            .OfType<MeshInstance3D>()
            .ToList();

        if (meshes.Count == 0)
            GD.PushWarning($"CharacterRig: No MeshInstance3D found under {path}.");

        return meshes;
    }

    protected virtual void CacheAnimations()
    {
        if (AnimPlayer != null)
            AnimationList = AnimPlayer.GetAnimationList();
        else
            AnimationList = Array.Empty<string>();
    }

    protected virtual void InitializeStateMachine()
    {
        if (AnimTree == null)
            return;

        var playbackVar = AnimTree.Get("parameters/playback");

        if (playbackVar.VariantType == Variant.Type.Object)
        {
            StateMachine = playbackVar.As<AnimationNodeStateMachinePlayback>();
            StateMachine?.Start(State.Idle.ToString());
        }
        else
        {
            GD.PushError("CharacterRig: AnimationTree playback parameter not found or invalid.");
        }
    }

    public virtual void PlayAnimation(string name, float blend = 0.2f)
    {
        if (AnimPlayer != null && AnimPlayer.HasAnimation(name))
            AnimPlayer.Play(name, customBlend: blend);
    }

    public virtual void PlayState(State state)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        _currentState = state;

        if (_currentState != state)
        {
            _lastStateChangeTime = now;
        }
        if (now - _lastStateChangeTime < StateChangeCooldown)
        {
            return;
        }

        if (StateMachine != null)
        {
            StateMachine.Travel(state.ToString());
            return;
        }

        // Fallback: AnimationPlayer-only rigs
        if (StateToAnimation.TryGetValue(state, out var animName))
        {
            PlayAnimation(animName);
        }
        else
        {
            GD.PushWarning($"CharacterRig: No animation mapping found for state {state}.");
        }
    }

    protected virtual void HideAllEquipment()
    {
        foreach (var slot in Equipment.Values)
            foreach (var mesh in slot)
                mesh.Visible = false;
    }

    public virtual void Equip(EquipmentSlot slot, string meshName)
    {
        if (!Equipment.TryGetValue(slot, out var meshes))
            return;

        foreach (var mesh in meshes)
            mesh.Visible = mesh.Name == meshName;
    }

    public virtual void Unequip(EquipmentSlot slot)
    {
        if (!Equipment.TryGetValue(slot, out var meshes))
            return;

        foreach (var mesh in meshes)
            mesh.Visible = false;
    }

    public virtual void ApplyLoadout(CharacterLoadout loadout)
    {
        if (loadout == null)
            return;

        if (!string.IsNullOrEmpty(loadout.HeadItem))
            Equip(EquipmentSlot.Head, loadout.HeadItem);
        else
            Unequip(EquipmentSlot.Head);

        if (!string.IsNullOrEmpty(loadout.LeftHandItem))
            Equip(EquipmentSlot.LeftHand, loadout.LeftHandItem);
        else
            Unequip(EquipmentSlot.LeftHand);

        if (!string.IsNullOrEmpty(loadout.RightHandItem))
            Equip(EquipmentSlot.RightHand, loadout.RightHandItem);
        else
            Unequip(EquipmentSlot.RightHand);

        if (!string.IsNullOrEmpty(loadout.TorsoItem))
            Equip(EquipmentSlot.Torso, loadout.TorsoItem);
        else
            Unequip(EquipmentSlot.Torso);
    }

    public virtual IReadOnlyList<string> GetEquipmentNames(EquipmentSlot slot)
    {
        if (!Equipment.TryGetValue(slot, out var meshes))
            return Array.Empty<string>();

        return (IReadOnlyList<string>)meshes.Select(m => m.Name).ToList();
    }

    public int GetBoneIndex(string boneName)
    {
        return Skeleton?.FindBone(boneName) ?? -1;
    }

    // -------------------- Animation Event Relay --------------------
    // These are called from animation tracks (Call Method track → call these)

    public void AnimEvent(string eventName)
    {
        EmitSignal(SignalName.AnimationEvent, eventName);

        // Optional typed relays
        switch (eventName)
        {
            case "attack_hit":
                EmitSignal(SignalName.AttackHit);
                break;
            case "footstep":
                EmitSignal(SignalName.Footstep);
                break;
            case "death":
                EmitSignal(SignalName.Death);
                break;
            case "attack_start":
                WeaponHitbox.Monitoring = true;
                break;
            case "attack_end":
                WeaponHitbox.Monitoring = false;
                break;
        }
    }
}
