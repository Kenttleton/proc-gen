using Godot;
using System;

public partial class ThirdPersonPlayer : CharacterBody3D
{
	// Movement parameters
	[ExportGroup("Movement")]
	[Export] public float WalkSpeed = 5.0f;
	[Export] public float SprintSpeed = 8.0f;
	[Export] public float Acceleration = 10.0f;
	[Export] public float Deceleration = 15.0f;
	[Export] public float RotationSpeed = 10.0f;

	// Jump parameters
	[ExportGroup("Jump")]
	[Export] public float JumpVelocity = 6.0f;
	[Export] public float Gravity = 15.0f;
	[Export] public float FallGravityMultiplier = 1.5f;

	// Camera parameters
	[ExportGroup("Camera")]
	[Export] public float MouseSensitivity = 0.003f;
	[Export] public float MinCameraAngle = -45.0f;
	[Export] public float MaxCameraAngle = 60.0f;
	[Export] public float CameraDistance = 5.0f;
	[Export] public float Fov = 75.0f;

	[ExportGroup("Visual")]
	[Export] public bool ShowDebugInfo = true;

	// Node references
	private SpringArm3D _springArm;
	private Camera3D _camera;
	private MeshInstance3D _playerMesh;
	private Node3D _cameraTarget; // Pivot point for camera rotation
	private Label3D _debugLabel;

	// State variables
	private float _cameraPitch = 0.0f;
	private Vector3 _currentVelocity = Vector3.Zero;

	private AudioStreamPlayer3D _footstepPlayer;
	private float _footstepTimer = 0.0f;
	private float _footstepInterval = 0.4f;

	public override void _Ready()
	{
		SetCollisionLayerValue(1, false);  // Player NOT on layer 1
		SetCollisionLayerValue(2, true);   // Player IS on layer 2
		SetCollisionMaskValue(1, true);    // Player DETECTS layer 1 (terrain)
		SetCollisionMaskValue(2, false);   // Player doesn't detect other players

		FloorStopOnSlope = true;
		FloorMaxAngle = Mathf.DegToRad(45);
		FloorSnapLength = 0.1f;
		SafeMargin = 0.001f;

		MotionMode = MotionModeEnum.Grounded;

		SetupPlayerBody();
		SetupCamera();

		if (ShowDebugInfo)
			SetupDebugLabel();

		// Capture mouse for camera control
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	private void SetupDebugLabel()
	{
		_debugLabel = new Label3D();
		_debugLabel.Position = new Vector3(0, 2.5f, 0);
		_debugLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
		_debugLabel.NoDepthTest = true;
		_debugLabel.FontSize = 24;
		AddChild(_debugLabel);
	}

	private void SetupPlayerBody()
	{
		// Clear existing shapes
		foreach (Node child in GetChildren())
		{
			if (child is CollisionShape3D || child is MeshInstance3D)
			{
				child.QueueFree();
			}
		}
		// Create collision shape
		var collisionShape = new CollisionShape3D();
		var capsuleShape = new CapsuleShape3D();
		capsuleShape.Radius = 0.5f;
		capsuleShape.Height = 2.0f;
		collisionShape.Shape = capsuleShape;
		collisionShape.Position = new Vector3(0, 0, 0); // Center the capsule on the body
		AddChild(collisionShape);

		// Create visual mesh (offset up so bottom is at origin)
		_playerMesh = new MeshInstance3D();
		var capsuleMesh = new CapsuleMesh();
		capsuleMesh.Radius = 0.5f;
		capsuleMesh.Height = 2.0f;
		_playerMesh.Mesh = capsuleMesh;
		_playerMesh.Position = new Vector3(0, 0, 0); // Center the capsule

		// Add material
		var material = new StandardMaterial3D();
		material.AlbedoColor = new Color(0.3f, 0.5f, 0.8f);
		_playerMesh.MaterialOverride = material;

		AddChild(_playerMesh);
	}

	private void SetupCamera()
	{
		// Create camera pivot point (rotates left/right)
		_cameraTarget = new Node3D();
		_cameraTarget.Position = new Vector3(0, 1.5f, 0); // Eye level
		AddChild(_cameraTarget);

		// Create spring arm (handles camera distance and collision)
		_springArm = new SpringArm3D();
		_springArm.SpringLength = CameraDistance;
		_springArm.Margin = 0.2f; // Prevents camera from clipping through walls

		// Add collision mask for camera
		_springArm.CollisionMask = 1; // Layer 1 (world geometry)

		_cameraTarget.AddChild(_springArm);

		// Create camera
		_camera = new Camera3D();
		_camera.Fov = Fov; // Field of view
		_springArm.AddChild(_camera);
	}

	public override void _Input(InputEvent @event)
	{
		// Handle mouse camera control
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			// Horizontal rotation (yaw)
			_cameraTarget.RotateY(-mouseMotion.Relative.X * MouseSensitivity);

			// Vertical rotation (pitch)
			_cameraPitch -= mouseMotion.Relative.Y * MouseSensitivity;
			_cameraPitch = Mathf.Clamp(_cameraPitch,
				Mathf.DegToRad(MinCameraAngle),
				Mathf.DegToRad(MaxCameraAngle));

			_springArm.Rotation = new Vector3(_cameraPitch, 0, 0);
		}

		// Toggle mouse capture with ESC
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (Input.MouseMode == Input.MouseModeEnum.Captured)
				Input.MouseMode = Input.MouseModeEnum.Visible;
			else
				Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;
		float deltaTime = (float)delta;

		// Apply gravity
		if (!IsOnFloor())
		{
			float gravityToApply = Gravity;

			// Fall faster when moving down (feels more responsive)
			if (velocity.Y < 0)
				gravityToApply *= FallGravityMultiplier;

			velocity.Y -= gravityToApply * deltaTime;
		}
		else
		{
			// Small downward force to keep player grounded on slopes
			velocity.Y = -2.0f;
		}

		// Handle jump
		if (Input.IsActionJustPressed("jump") && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}

		// Get input direction
		Vector2 inputDir = Input.GetVector("left", "right", "up", "down");

		// Calculate movement direction relative to camera
		Vector3 direction = CalculateMovementDirection(inputDir);

		// Determine target speed
		bool isSprinting = Input.IsActionPressed("sprint");
		float targetSpeed = isSprinting ? SprintSpeed : WalkSpeed;

		// Calculate target velocity (horizontal only)
		Vector3 targetVelocity = direction * targetSpeed;

		// Smoothly interpolate to target velocity
		float accelerationRate = direction.Length() > 0 ? Acceleration : Deceleration;

		velocity.X = Mathf.Lerp(velocity.X, targetVelocity.X, accelerationRate * deltaTime);
		velocity.Z = Mathf.Lerp(velocity.Z, targetVelocity.Z, accelerationRate * deltaTime);

		// Rotate player body to face movement direction
		if (direction.Length() > 0.1f)
		{
			RotatePlayerToDirection(direction, deltaTime);
		}

		// Before MoveAndSlide, check what's below us with a raycast
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(
			GlobalPosition,
			GlobalPosition + Vector3.Down * 55.0f,
			1
		);

		var result = spaceState.IntersectRay(query);
		if (result.Count > 0)
		{
			GD.Print($"Raycast hit: {result["collider"]}, at position {result["position"]}");
		}

		// Apply velocity
		Velocity = velocity;
		MoveAndSlide();
		// Update debug info
		if (ShowDebugInfo && _debugLabel != null)
		{
			float speed = new Vector3(Velocity.X, 0, Velocity.Z).Length();
			for (int i = 0; i < GetSlideCollisionCount(); i++)
			{
				var collision = GetSlideCollision(i);
				GD.Print($"Collided with: {collision.GetCollider()}");
			}
			_debugLabel.Text = $"Speed: {speed:F1}\nY Vel: {Velocity.Y:F1}\n{(IsOnFloor() ? "Grounded" : "Airborne")}";
		}

		// Visual feedback: squash and stretch on landing
		UpdateVisualFeedback(delta);
	}

	private Vector3 CalculateMovementDirection(Vector2 inputDir)
	{
		// Get camera's forward and right directions (on horizontal plane)
		Vector3 cameraForward = -_cameraTarget.GlobalTransform.Basis.Z;
		cameraForward.Y = 0;
		cameraForward = cameraForward.Normalized();

		Vector3 cameraRight = _cameraTarget.GlobalTransform.Basis.X;
		cameraRight.Y = 0;
		cameraRight = cameraRight.Normalized();

		// Calculate movement direction
		Vector3 direction = (cameraRight * inputDir.X + cameraForward * inputDir.Y).Normalized();

		return direction;
	}

	private void RotatePlayerToDirection(Vector3 direction, float deltaTime)
	{
		// Calculate target rotation
		float targetAngle = Mathf.Atan2(direction.X, direction.Z);

		// Get current rotation
		float currentAngle = Rotation.Y;

		// Smoothly rotate towards target
		float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, RotationSpeed * deltaTime);

		// Apply rotation (only Y axis)
		Rotation = new Vector3(Rotation.X, newAngle, Rotation.Z);
	}

	private bool _wasInAir = false;

	private void UpdateVisualFeedback(double delta)
	{
		// Landing squash effect
		if (!_wasInAir && !IsOnFloor())
		{
			_wasInAir = true;
		}
		else if (_wasInAir && IsOnFloor())
		{
			_wasInAir = false;
			// Quick squash and recover
			CreateLandingEffect();
		}

		// Lean in movement direction
		if (_playerMesh != null)
		{
			Vector3 horizontalVelocity = new Vector3(Velocity.X, 0, Velocity.Z);
			float speed = horizontalVelocity.Length();

			if (speed > 0.5f)
			{
				// Slight forward lean when moving
				Vector3 localVelocity = GlobalTransform.Basis.Inverse() * horizontalVelocity;
				float forwardTilt = Mathf.Clamp(localVelocity.Z * 0.05f, -0.2f, 0.2f);

				_playerMesh.Rotation = new Vector3(
					Mathf.Lerp(_playerMesh.Rotation.X, forwardTilt, 5.0f * (float)delta),
					_playerMesh.Rotation.Y,
					_playerMesh.Rotation.Z
				);
			}
			else
			{
				// Return to neutral
				_playerMesh.Rotation = new Vector3(
					Mathf.Lerp(_playerMesh.Rotation.X, 0, 5.0f * (float)delta),
					_playerMesh.Rotation.Y,
					_playerMesh.Rotation.Z
				);
			}
		}
	}

	private void CreateLandingEffect()
	{
		if (_playerMesh == null) return;

		// Create a quick scale tween for squash effect
		var tween = CreateTween();
		tween.TweenProperty(_playerMesh, "scale", new Vector3(1.2f, 0.8f, 1.2f), 0.1);
		tween.TweenProperty(_playerMesh, "scale", Vector3.One, 0.15);
	}

	private void PlayFootstep()
	{
		// You'll need to load actual audio files
		// For now, this just shows the structure

		// var sound = GD.Load<AudioStream>("res://sounds/footstep.wav");
		// _footstepPlayer.Stream = sound;
		// _footstepPlayer.Play();
	}
}