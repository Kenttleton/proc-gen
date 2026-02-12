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
    [Signal] public delegate void HurtboxHitEventHandler(Area3D hitbox, BodyPart part, float damageMultiplier);

    [ExportGroup("Debug")]
    [Export] public bool EnableDebugOutput = false;
    [Export] public bool ShowHurtboxes = false;
    [Export] public bool ShowHitboxes = false;

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
    [Export] public NodePath HitboxesRootPath;

    [ExportSubgroup("Collision Layers")]
    [Export] public uint HurtboxLayer = 4;      // Layer 4 = hurtboxes
    [Export] public uint HitboxLayer = 5;       // Layer 5 = hitboxes
    [Export] public uint HurtboxMask = 32;      // Mask bit 5 (detects hitboxes)
    [Export] public uint HitboxMask = 16;

    protected CollisionShape3D BodyCollider;
    protected Node3D HurtboxesRoot;
    protected Node3D HitboxesRoot;

    protected Dictionary<BodyPart, List<HurtboxData>> HurtboxesByPart = new();
    protected Dictionary<EquipmentSlot, List<HitboxData>> HitboxesBySlot = new();

    protected List<CollisionShape3D> Hurtboxes = new();
    protected List<CollisionShape3D> Hitboxes = new();

    protected AnimationPlayer AnimPlayer;
    protected AnimationTree AnimTree;
    protected AnimationNodeStateMachinePlayback StateMachine;
    private State _currentState = State.Idle;
    private double _lastStateChangeTime = 0;
    protected Skeleton3D Skeleton;

    public enum BodyPart
    {
        Head,       // 2x damage
        Torso,      // 1x damage
        Arms,       // 0.7x damage
        Legs,       // 0.8x damage
        Hands,      // 0.5x damage
        Feet        // 0.5x damage
    }
    private static readonly Dictionary<BodyPart, float> DamageMultipliers = new()
    {
        { BodyPart.Head, 2.0f },
        { BodyPart.Torso, 1.0f },
        { BodyPart.Arms, 0.7f },
        { BodyPart.Legs, 0.8f },
        { BodyPart.Hands, 0.5f },
        { BodyPart.Feet, 0.5f }
    };
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

    protected class HurtboxData
    {
        public Area3D Area;
        public CollisionShape3D Shape;
        public BodyPart Part;
        public MeshInstance3D SourceMesh;
        public MeshInstance3D DebugMesh;  // For visualization
    }

    protected class HitboxData
    {
        public Area3D Area;
        public CollisionShape3D Shape;
        public MeshInstance3D SourceMesh;
        public MeshInstance3D DebugMesh;  // For visualization
        public bool IsActive;  // Enabled/disabled during attacks
    }

    public override void _Ready()
    {
        CacheNodes();
        CacheEquipment();
        CacheAnimations();
        InitializeStateMachine();
        HideAllEquipment();
        ApplyLoadout(DefaultLoadout);
        AutoSizeMovementCapsule();
        GenerateHurtboxesFromMeshes();
        GenerateWeaponHitboxes();
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

    #region Movement Capsule Auto-Sizing
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

    #endregion

    #region Hurtbox Generation
    /// <summary>
    /// Generates hurtboxes for all body part meshes.
    /// LEARNING: Hurtboxes are collision areas that detect incoming damage to this character.
    /// Each body part gets its own hurtbox with specific damage multipliers.
    /// </summary>
    protected virtual void GenerateHurtboxesFromMeshes()
    {
        HurtboxesRoot = GetNodeOrNull<Node3D>(HurtboxesRootPath);
        if (HurtboxesRoot == null)
        {
            // Create root node if it doesn't exist
            HurtboxesRoot = new Node3D();
            HurtboxesRoot.Name = "Hurtboxes";
            AddChild(HurtboxesRoot);
            GD.Print("CharacterRig: Created Hurtboxes root node");
        }

        foreach (var partList in HurtboxesByPart.Values)
            foreach (var data in partList)
                data.Area.QueueFree();

        HurtboxesByPart.Clear();

        // Gather all body meshes (ignore weapons/clothes)
        var meshes = GetChildrenRecursive<MeshInstance3D>(this);
        var bodyMeshes = meshes.Where(m =>
            !m.Name.ToString().ToLower().Contains("sword") &&
            !m.Name.ToString().ToLower().Contains("cape") &&
            !m.Name.ToString().ToLower().Contains("shield") &&
            !m.Name.ToString().ToLower().Contains("helmet") &&
            m.Visible &&
            m.Mesh != null
        ).ToList();

        if (EnableDebugOutput)
            GD.Print($"CharacterRig: Found {bodyMeshes.Count} body meshes for hurtbox generation");

        foreach (var mesh in bodyMeshes)
        {
            BodyPart part = DetermineBodyPart(mesh.Name.ToString());
            GenerateHurtboxForMesh(mesh, part);
        }

        if (EnableDebugOutput)
            GD.Print($"CharacterRig: Generated {HurtboxesByPart.Values.Sum(list => list.Count)} hurtboxes");
    }

    /// <summary>
    /// Determines which body part a mesh represents based on its name.
    /// LEARNING: Naming conventions are crucial! Mesh names should include keywords
    /// like "head", "arm_l", "leg_r", etc.
    /// </summary>
    private BodyPart DetermineBodyPart(string meshName)
    {
        string name = meshName.ToLower();

        // Check for specific body parts
        if (name.Contains("head") || name.Contains("skull") || name.Contains("face"))
            return BodyPart.Head;

        if (name.Contains("hand") || name.Contains("palm") || name.Contains("finger"))
            return BodyPart.Hands;

        if (name.Contains("foot") || name.Contains("feet") || name.Contains("toe"))
            return BodyPart.Feet;

        if (name.Contains("arm") || name.Contains("elbow") || name.Contains("shoulder") ||
            name.Contains("forearm") || name.Contains("upperarm"))
            return BodyPart.Arms;

        if (name.Contains("leg") || name.Contains("thigh") || name.Contains("calf") ||
            name.Contains("knee") || name.Contains("shin"))
            return BodyPart.Legs;

        // Default to torso for chest, spine, pelvis, etc.
        return BodyPart.Torso;
    }

    /// <summary>
    /// Creates a single hurtbox area for a specific mesh.
    /// LEARNING: We use Area3D (not CharacterBody3D or StaticBody3D) because:
    /// - Area3D detects overlaps without physics simulation
    /// - Perfect for hit detection that doesn't need physical collision
    /// - Lighter weight than physics bodies
    /// </summary>
    private void GenerateHurtboxForMesh(MeshInstance3D mesh, BodyPart part)
    {
        // Create Area3D for detection
        var area = new Area3D();
        area.Name = $"{mesh.Name}_Hurtbox_{part}";
        area.CollisionLayer = HurtboxLayer;  // What layer this area is on
        area.CollisionMask = HurtboxMask;    // What layers this area detects
        area.Monitorable = true;             // Other areas can detect this
        area.Monitoring = true;              // This area detects others

        HurtboxesRoot.AddChild(area);

        // Create collision shape from mesh
        var collisionShape = new CollisionShape3D();

        // LEARNING: Shape generation strategies:
        // 1. ConvexShape - Fast, works for most organic shapes
        // 2. ConcaveShape - More accurate but slower, use for complex geometry
        // 3. Simplified shapes (capsule/box) - Fastest but least accurate

        // We'll use convex by default (good balance of performance and accuracy)
        collisionShape.Shape = mesh.Mesh.CreateConvexShape();
        area.AddChild(collisionShape);

        // Match the area's transform to the mesh
        // LEARNING: This ensures the hurtbox moves/rotates with animated bones!
        area.GlobalTransform = mesh.GlobalTransform;

        // Create debug visualization if enabled
        MeshInstance3D debugMesh = null;
        if (ShowHurtboxes)
        {
            debugMesh = CreateDebugMesh(mesh, new Color(1, 0, 0, 0.3f)); // Red semi-transparent
            area.AddChild(debugMesh);
        }

        // Store hurtbox data
        var data = new HurtboxData
        {
            Area = area,
            Shape = collisionShape,
            Part = part,
            SourceMesh = mesh,
            DebugMesh = debugMesh
        };

        if (!HurtboxesByPart.ContainsKey(part))
            HurtboxesByPart[part] = new List<HurtboxData>();

        HurtboxesByPart[part].Add(data);

        // Connect signal to handle hits
        area.AreaEntered += (otherArea) => OnHurtboxHit(data, otherArea);

        if (EnableDebugOutput)
            GD.Print($"CharacterRig: Created hurtbox for {mesh.Name} → {part} (multiplier: {DamageMultipliers[part]}x)");
    }

    /// <summary>
    /// Called when an enemy's hitbox overlaps with this character's hurtbox.
    /// </summary>
    private void OnHurtboxHit(HurtboxData hurtbox, Area3D hitbox)
    {
        float multiplier = DamageMultipliers[hurtbox.Part];

        if (EnableDebugOutput)
            GD.Print($"CharacterRig: Hurtbox hit! Part: {hurtbox.Part}, Multiplier: {multiplier}x");

        EmitSignal(SignalName.HurtboxHit, hitbox, (int)hurtbox.Part, multiplier);
    }

    // protected void CacheWeaponHitbox()
    // {
    //     WeaponHitbox = GetNodeOrNull<Area3D>(WeaponHitboxPath);
    //     if (WeaponHitbox != null)
    //         WeaponHitbox.Monitoring = false; // off by default
    // }
    #endregion

    #region Weapon Hitbox Generation

    /// <summary>
    /// Generates hitboxes for equipped weapons.
    /// LEARNING: Hitboxes are the "attack zones" - when you swing a sword,
    /// the hitbox is what detects if you hit an enemy.
    /// </summary>
    protected virtual void GenerateWeaponHitboxes()
    {
        HitboxesRoot = GetNodeOrNull<Node3D>(HitboxesRootPath);
        if (HitboxesRoot == null)
        {
            HitboxesRoot = new Node3D();
            HitboxesRoot.Name = "Hitboxes";
            AddChild(HitboxesRoot);
            GD.Print("CharacterRig: Created Hitboxes root node");
        }

        // Clear existing hitboxes
        foreach (var slotList in HitboxesBySlot.Values)
            foreach (var data in slotList)
                data.Area.QueueFree();

        HitboxesBySlot.Clear();

        // Generate hitboxes for each equipment slot
        foreach (var kvp in Equipment)
        {
            EquipmentSlot slot = kvp.Key;
            List<MeshInstance3D> meshes = kvp.Value;

            foreach (var mesh in meshes)
            {
                // Only create hitboxes for weapon meshes
                if (mesh.Visible && IsWeaponMesh(mesh.Name.ToString()))
                {
                    GenerateHitboxForMesh(mesh, slot);
                }
            }
        }

        if (EnableDebugOutput)
            GD.Print($"CharacterRig: Generated {HitboxesBySlot.Values.Sum(list => list.Count)} weapon hitboxes");
    }

    /// <summary>
    /// Determines if a mesh is a weapon based on naming conventions.
    /// </summary>
    private bool IsWeaponMesh(string meshName)
    {
        string name = meshName.ToLower();
        return name.Contains("sword") || name.Contains("axe") ||
               name.Contains("hammer") || name.Contains("mace") ||
               name.Contains("blade") || name.Contains("weapon");
    }

    /// <summary>
    /// Creates a hitbox for a weapon mesh.
    /// </summary>
    private void GenerateHitboxForMesh(MeshInstance3D mesh, EquipmentSlot slot)
    {
        var area = new Area3D();
        area.Name = $"{mesh.Name}_Hitbox";
        area.CollisionLayer = HitboxLayer;
        area.CollisionMask = HitboxMask;
        area.Monitorable = true;
        area.Monitoring = false;

        HitboxesRoot.AddChild(area);

        var collisionShape = new CollisionShape3D();

        Shape3D shape = CreateBoxWeaponHitbox(mesh, slot);

        collisionShape.Shape = shape;
        area.AddChild(collisionShape);

        // Match transform to weapon mesh
        area.GlobalTransform = mesh.GlobalTransform;
        area.RotateY(Mathf.Pi / 2); // Rotate to align with forward direction

        MeshInstance3D debugMesh = null;
        if (ShowHitboxes)
        {
            debugMesh = CreateDebugMesh(mesh, new Color(0, 1, 0, 0.3f));
            area.AddChild(debugMesh);
        }

        var data = new HitboxData
        {
            Area = area,
            Shape = collisionShape,
            SourceMesh = mesh,
            DebugMesh = debugMesh,
            IsActive = false
        };

        if (!HitboxesBySlot.ContainsKey(slot))
            HitboxesBySlot[slot] = new List<HitboxData>();

        HitboxesBySlot[slot].Add(data);

        if (EnableDebugOutput)
            GD.Print($"CharacterRig: Created swept hitbox for weapon {mesh.Name} in slot {slot}");
    }

    private Shape3D CreateSweptWeaponHitbox(MeshInstance3D weaponMesh, EquipmentSlot slot)
    {
        // Get weapon bounds
        Aabb weaponAabb = weaponMesh.GetAabb();

        // Find the hand bone attachment for this slot
        BoneAttachment3D handAttachment = null;

        if (slot == EquipmentSlot.RightHand && RightHandSlotPath != null)
            handAttachment = GetNodeOrNull<BoneAttachment3D>(RightHandSlotPath);
        else if (slot == EquipmentSlot.LeftHand && LeftHandSlotPath != null)
            handAttachment = GetNodeOrNull<BoneAttachment3D>(LeftHandSlotPath);

        // Calculate hitbox dimensions
        float weaponLength = weaponAabb.Size.Length(); // Total length of weapon
        float weaponThickness = Mathf.Min(weaponAabb.Size.X, weaponAabb.Size.Z);

        // Estimate arm length from skeleton
        float armLength = 0.6f; // Default estimate
        if (Skeleton != null && handAttachment != null)
        {
            // Try to get actual arm length from skeleton
            int handBone = Skeleton.FindBone(handAttachment.BoneName);
            if (handBone != -1)
            {
                int shoulderBone = Skeleton.GetBoneParent(handBone);
                if (shoulderBone != -1)
                {
                    // Calculate distance from shoulder to hand
                    Transform3D shoulderTransform = Skeleton.GetBoneGlobalPose(shoulderBone);
                    Transform3D handTransform = Skeleton.GetBoneGlobalPose(handBone);
                    armLength = shoulderTransform.Origin.DistanceTo(handTransform.Origin);
                }
            }
        }

        // Create capsule that covers full swing arc
        // Height = arm length + weapon length (full reach)
        // Radius = weapon thickness + swing arc allowance
        var capsule = new CapsuleShape3D();
        capsule.Height = armLength + weaponLength;
        capsule.Radius = Mathf.Max(weaponThickness * 0.5f, 0.15f); // Minimum 15cm radius

        // Adjust radius for swing arc (weapons swing in an arc, not straight line)
        capsule.Radius *= 1.5f; // 50% wider to account for arc

        if (EnableDebugOutput)
        {
            GD.Print($"  Swept hitbox: Height={capsule.Height:F2} (arm={armLength:F2} + weapon={weaponLength:F2}), Radius={capsule.Radius:F2}");
        }

        return capsule;
    }

    private Shape3D CreateBoxWeaponHitbox(MeshInstance3D weaponMesh, EquipmentSlot slot)
    {
        Aabb weaponAabb = weaponMesh.GetAabb();

        // Estimate reach including arm
        float armReach = 0.6f; // Approximate arm length
        float weaponLength = weaponAabb.Size.Y; // Assume weapon extends along Y axis

        float totalReach = armReach + weaponLength;
        float swingWidth = totalReach * 0.8f; // Arc width
        float hitboxThickness = Mathf.Max(weaponAabb.Size.X, weaponAabb.Size.Z) * 1.5f;

        var box = new BoxShape3D();
        box.Size = new Vector3(swingWidth, hitboxThickness, totalReach);

        if (EnableDebugOutput)
        {
            GD.Print($"  Box hitbox: Size={box.Size}");
        }

        return box;
    }

    /// <summary>
    /// Enable weapon hitboxes during attack animations.
    /// Call this from animation events: "attack_start"
    /// </summary>
    public void EnableWeaponHitboxes()
    {
        foreach (var slotList in HitboxesBySlot.Values)
        {
            foreach (var data in slotList)
            {
                if (data.SourceMesh.Visible) // Only enable if weapon is visible/equipped
                {
                    data.Area.Monitoring = true;
                    data.IsActive = true;

                    if (EnableDebugOutput)
                        GD.Print($"CharacterRig: Enabled hitbox for {data.SourceMesh.Name}");
                }
            }
        }
    }

    /// <summary>
    /// Disable weapon hitboxes after attack animations.
    /// Call this from animation events: "attack_end"
    /// </summary>
    public void DisableWeaponHitboxes()
    {
        foreach (var slotList in HitboxesBySlot.Values)
        {
            foreach (var data in slotList)
            {
                data.Area.Monitoring = false;
                data.IsActive = false;
            }
        }

        if (EnableDebugOutput)
            GD.Print("CharacterRig: Disabled all weapon hitboxes");
    }

    #endregion

    #region Debug Visualization

    /// <summary>
    /// Creates a semi-transparent mesh for visualizing collision shapes.
    /// LEARNING: This is super helpful for debugging! You can see exactly where
    /// your hitboxes and hurtboxes are during gameplay.
    /// </summary>
    private MeshInstance3D CreateDebugMesh(MeshInstance3D sourceMesh, Color color)
    {
        var debugMesh = new MeshInstance3D();
        debugMesh.Mesh = sourceMesh.Mesh;
        debugMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        // Create transparent material
        var material = new StandardMaterial3D();
        material.AlbedoColor = color;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // See from both sides

        debugMesh.MaterialOverride = material;

        return debugMesh;
    }

    /// <summary>
    /// Toggle hurtbox visualization at runtime.
    /// </summary>
    public void ToggleHurtboxVisuals(bool visible)
    {
        ShowHurtboxes = visible;

        foreach (var partList in HurtboxesByPart.Values)
        {
            foreach (var data in partList)
            {
                if (visible && data.DebugMesh == null)
                {
                    data.DebugMesh = CreateDebugMesh(data.SourceMesh, new Color(1, 0, 0, 0.3f));
                    data.Area.AddChild(data.DebugMesh);
                }
                else if (!visible && data.DebugMesh != null)
                {
                    data.DebugMesh.QueueFree();
                    data.DebugMesh = null;
                }
            }
        }
    }

    /// <summary>
    /// Toggle hitbox visualization at runtime.
    /// </summary>
    public void ToggleHitboxVisuals(bool visible)
    {
        ShowHitboxes = visible;

        foreach (var slotList in HitboxesBySlot.Values)
        {
            foreach (var data in slotList)
            {
                if (visible && data.DebugMesh == null)
                {
                    data.DebugMesh = CreateDebugMesh(data.SourceMesh, new Color(0, 1, 0, 0.3f));
                    data.Area.AddChild(data.DebugMesh);
                }
                else if (!visible && data.DebugMesh != null)
                {
                    data.DebugMesh.QueueFree();
                    data.DebugMesh = null;
                }
            }
        }
    }

    #endregion

    #region Update Loop

    public override void _Process(double delta)
    {
        // UpdateHurtboxTransforms();
        // UpdateHitboxTransforms();
    }

    private void UpdateHurtboxTransforms()
    {
        var partsCopy = new List<BodyPart>(HurtboxesByPart.Keys);

        foreach (var part in partsCopy)
        {
            if (!HurtboxesByPart.ContainsKey(part))
                continue;

            var dataList = new List<HurtboxData>(HurtboxesByPart[part]);

            foreach (var data in dataList)
            {
                // FIX: Check if nodes are still valid
                if (!IsInstanceValid(data.Area) || !IsInstanceValid(data.SourceMesh))
                {
                    // Remove invalid data
                    HurtboxesByPart[part].Remove(data);
                    continue;
                }

                try
                {
                    // Match the area's transform to the source mesh's current transform
                    data.Area.GlobalTransform = data.SourceMesh.GlobalTransform;
                }
                catch (ObjectDisposedException)
                {
                    // Object was disposed between check and use
                    HurtboxesByPart[part].Remove(data);
                    GD.PushWarning($"CharacterRig: Hurtbox for {part} was disposed");
                }
            }
        }
    }

    private void UpdateHitboxTransforms()
    {
        var slotsCopy = new List<EquipmentSlot>(HitboxesBySlot.Keys);

        foreach (var slot in slotsCopy)
        {
            if (!HitboxesBySlot.ContainsKey(slot))
                continue;

            var dataList = new List<HitboxData>(HitboxesBySlot[slot]);

            foreach (var data in dataList)
            {
                if (!IsInstanceValid(data.Area) || !IsInstanceValid(data.SourceMesh))
                {
                    HitboxesBySlot[slot].Remove(data);
                    continue;
                }

                try
                {
                    data.Area.GlobalTransform = data.SourceMesh.GlobalTransform;
                }
                catch (ObjectDisposedException)
                {
                    HitboxesBySlot[slot].Remove(data);
                    GD.PushWarning($"CharacterRig: Hitbox for {slot} was disposed");
                }
            }
        }
    }

    #endregion

    #region Equipment & Animations (existing code with updates)

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

        // NEW: Regenerate hitboxes when equipment changes
        GenerateWeaponHitboxes();
    }

    public virtual void Unequip(EquipmentSlot slot)
    {
        if (!Equipment.TryGetValue(slot, out var meshes))
            return;

        foreach (var mesh in meshes)
            mesh.Visible = false;

        // NEW: Regenerate hitboxes when equipment changes
        GenerateWeaponHitboxes();
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

    public virtual void PlayAnimation(string name, float blend = 0.2f)
    {
        if (AnimPlayer != null && AnimPlayer.HasAnimation(name))
            AnimPlayer.Play(name, customBlend: blend);
    }

    public virtual void PlayState(State state)
    {
        double now = Time.GetTicksMsec() / 1000.0;

        if (_currentState != state)
        {
            _lastStateChangeTime = now;
            _currentState = state;
        }

        if (now - _lastStateChangeTime < StateChangeCooldown)
            return;

        if (StateMachine != null)
        {
            StateMachine.Travel(state.ToString());
            return;
        }

        if (StateToAnimation.TryGetValue(state, out var animName))
        {
            PlayAnimation(animName);
        }
        else
        {
            GD.PushWarning($"CharacterRig: No animation mapping found for state {state}.");
        }
    }

    // -------------------- Animation Event Relay --------------------

    public void AnimEvent(string eventName)
    {
        EmitSignal(SignalName.AnimationEvent, eventName);

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
                EnableWeaponHitboxes();  // NEW: Enable hitboxes
                break;
            case "attack_end":
                DisableWeaponHitboxes(); // NEW: Disable hitboxes
                break;
        }
    }

    #endregion
}
