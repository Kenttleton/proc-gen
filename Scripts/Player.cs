using Godot;
using Godot.Collections;
using System;

public partial class Player : CharacterBody3D
{
	[ExportGroup("Movement")]
	[Export] public float WalkSpeed = 5.0f;
	[Export] public float SprintSpeed = 8.0f;
	[Export] public float Acceleration = 10.0f;
	[Export] public float Deceleration = 15.0f;
	[Export] public float RotationSpeed = 12.0f;
	[Export] public float RotationSmoothTime = 0.15f;
	[Export] public float SnapThreshold = 0.02f;
	private Vector3 _targetDirection = Vector3.Zero;
	private float _currentRotationVelocity = 0.0f;

	[ExportSubgroup("Jumping")]
	[Export] public float JumpVelocity = 4.5f;
	[Export] public float Gravity = 10.0f;
	[Export] public float FallGravityMultiplier = 1.5f;
	[Export] public float MaxFallSpeed = 20.0f;

	[ExportGroup("Camera")]
	[Export] public float MouseSensitivity = 0.003f;
	[Export] public float MinCameraAngle = -45.0f;
	[Export] public float MaxCameraAngle = 60.0f;
	[Export] public float CameraDistance = 5.0f;
	[Export] public float Fov = 75.0f;

	[ExportGroup("Player Node")]
	[Export] public CharacterRig PlayerNode;

	private SpringArm3D _springArm;
	private Camera3D _camera;
	private MeshInstance3D _playerMesh;
	private Node3D _cameraTarget;
	private float _cameraPitch = 0.0f;

	[ExportGroup("Debug")]
	[Export] public bool ShowDebugInfo = true;
	public Label3D _debugLabel = new Label3D();

	public override void _Ready()
	{
		if (ShowDebugInfo)
			SetupDebugLabel();

		SetupCamera();
		Input.MouseMode = Input.MouseModeEnum.Captured;
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

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += GetGravity() * deltaTime;
			float gravityToApply = Gravity;

			// Fall faster when moving down (feels more responsive)
			if (velocity.Y < 0)
				gravityToApply *= FallGravityMultiplier;

			velocity.Y -= gravityToApply * deltaTime;
			// Clamp fall speed to terminal velocity
			velocity.Y = Mathf.Clamp(velocity.Y, -MaxFallSpeed, float.MaxValue);
		}
		else
		{
			// Small downward force to keep player grounded on slopes
			velocity.Y = -2.0f;
		}

		// Handle Jump.
		if (Input.IsActionJustPressed("jump") && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}



		// Get the input direction and handle the movement/deceleration.
		Vector2 inputDir = Input.GetVector("left", "right", "up", "down");
		Vector3 inputDirection = CalculateMovementDirection(inputDir);

		bool isMoving = inputDir.Length() > 0.1f;
		bool isSprinting = Input.IsActionPressed("sprint");
		float targetSpeed = isSprinting ? SprintSpeed : WalkSpeed;
		//Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		if (inputDirection != Vector3.Zero)
		{
			_targetDirection = _targetDirection.Lerp(inputDirection, Acceleration * deltaTime);
			_targetDirection = _targetDirection.Normalized();

			velocity.X = inputDirection.X * targetSpeed;
			velocity.Z = inputDirection.Z * targetSpeed;

			RotatePlayerToDirectionSmoothDampAngle(_targetDirection, deltaTime);
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, targetSpeed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, targetSpeed);
		}

		Velocity = velocity;
		MoveAndSlide();
		UpdateAnimationState(isMoving, isSprinting);

		if (ShowDebugInfo && _debugLabel != null)
		{
			float speed = new Vector3(Velocity.X, 0, Velocity.Z).Length();
			_debugLabel.Text = $"Speed: {speed:F1}\nY Vel: {Velocity.Y:F1}\n{(IsOnFloor() ? "Grounded" : "Airborne")}";
		}
	}

	private Vector3 CalculateMovementDirection(Vector2 inputDir)
	{
		Vector3 cameraForward = _cameraTarget.GlobalTransform.Basis.Z;
		cameraForward.Y = 0;
		cameraForward = cameraForward.Normalized();

		Vector3 cameraRight = _cameraTarget.GlobalTransform.Basis.X;
		cameraRight.Y = 0;
		cameraRight = cameraRight.Normalized();

		Vector3 direction = (cameraRight * inputDir.X + cameraForward * inputDir.Y).Normalized();

		return direction;
	}

	private void RotatePlayerToDirection(Vector3 direction, float deltaTime)
	{
		if (direction.LengthSquared() < 0.01f)
			return;

		// Calculate target rotation
		float targetAngle = Mathf.Atan2(direction.X, direction.Z);

		// Get current rotation
		float currentAngle = PlayerNode != null ? PlayerNode.Rotation.Y : Rotation.Y;

		// Smooth rotation using exponential decay
		float smoothedAngle = Mathf.LerpAngle(
			currentAngle,
			targetAngle,
			1.0f - Mathf.Exp(-RotationSpeed * deltaTime)
		);

		if (PlayerNode != null)
		{
			PlayerNode.Rotation = new Vector3(PlayerNode.Rotation.X, smoothedAngle, PlayerNode.Rotation.Z);
		}
	}

	private void RotatePlayerToDirectionSmoothDampAngle(Vector3 direction, float deltaTime)
	{
		if (direction.LengthSquared() < 0.01f)
			return;

		float targetAngle = Mathf.Atan2(direction.X, direction.Z);
		float currentAngle = PlayerNode != null ? PlayerNode.Rotation.Y : Rotation.Y;

		// SmoothDamp for angular velocity
		float newAngle = SmoothDampAngle(
			currentAngle,
			targetAngle,
			ref _currentRotationVelocity,
			RotationSmoothTime,
			RotationSpeed,
			deltaTime
		);

		if (PlayerNode != null)
		{
			PlayerNode.Rotation = new Vector3(PlayerNode.Rotation.X, newAngle, PlayerNode.Rotation.Z);
		}
	}

	// Custom SmoothDampAngle implementation (like Unity's)
	private float SmoothDampAngle(float current, float target, ref float currentVelocity,
		float smoothTime, float maxSpeed, float deltaTime)
	{
		// Normalize angles to -PI to PI range
		target = NormalizeAngle(target);
		current = NormalizeAngle(current);

		// Find shortest rotation
		float delta = Mathf.AngleDifference(current, target);

		// SmoothDamp the difference
		float result = SmoothDamp(0, delta, ref currentVelocity, smoothTime, maxSpeed, deltaTime);

		return current + result;
	}

	private float SmoothDamp(float current, float target, ref float currentVelocity,
		float smoothTime, float maxSpeed, float deltaTime)
	{
		smoothTime = Mathf.Max(0.0001f, smoothTime);
		float omega = 2.0f / smoothTime;
		float x = omega * deltaTime;
		float exp = 1.0f / (1.0f + x + 0.48f * x * x + 0.235f * x * x * x);

		float change = current - target;
		float originalTo = target;

		float maxChange = maxSpeed * smoothTime;
		change = Mathf.Clamp(change, -maxChange, maxChange);
		target = current - change;

		float temp = (currentVelocity + omega * change) * deltaTime;
		currentVelocity = (currentVelocity - omega * temp) * exp;
		float output = target + (change + temp) * exp;

		if (originalTo - current > 0.0f == output > originalTo)
		{
			output = originalTo;
			currentVelocity = (output - originalTo) / deltaTime;
		}

		return output;
	}

	private float NormalizeAngle(float angle)
	{
		while (angle > Mathf.Pi) angle -= Mathf.Tau;
		while (angle < -Mathf.Pi) angle += Mathf.Tau;
		return angle;
	}

	private void UpdateAnimationState(bool isMoving, bool isSprinting)
	{
		if (PlayerNode == null)
			return;
		if (!isMoving && IsOnFloor())
		{
			PlayerNode.PlayState(CharacterRig.State.Idle);
			return;
		}
		if (isMoving && IsOnFloor())
		{
			if (isSprinting)
			{
				PlayerNode.PlayState(CharacterRig.State.Running);
			}
			else
			{
				PlayerNode.PlayState(CharacterRig.State.Walking);
			}
			return;
		}
		if (!IsOnFloor())
		{
			PlayerNode.PlayState(CharacterRig.State.Jumping);
			return;
		}
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

	private Dictionary RaycastDetection(PhysicsRayQueryParameters3D query)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		var result = spaceState.IntersectRay(query);
		return result;
	}
}
