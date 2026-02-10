using Godot;
using System;
using System.Collections.Generic;

public partial class Compass : Control
{
	private Panel _compassBar;
	private Control _compassContainer;
	private Camera3D _camera;

	// Cardinal direction labels (N, E, S, W)
	private Dictionary<float, Label> _compassLabels = new();

	// POI markers
	private Dictionary<Vector2, Control> _markers = new();

	// Compass settings
	private const float COMPASS_WIDTH_PERCENT = 0.8f; // 80% of screen width
	private const float COMPASS_HEIGHT = 40f;
	private const float COMPASS_Y_OFFSET = 10f;
	private const float MARKER_SIZE = 8f;

	public Compass(Camera3D camera)
	{
		_camera = camera;
		Name = "Compass";
	}

	public override void _Ready()
	{
		// Position this Control at top of screen
		AnchorLeft = 0;
		AnchorRight = 1;
		AnchorTop = 0;
		AnchorBottom = 0;
		OffsetBottom = COMPASS_HEIGHT + COMPASS_Y_OFFSET * 2;

		BuildCompass();
	}

	private void BuildCompass()
	{
		// Main compass bar (background panel)
		_compassBar = new Panel();
		_compassBar.AnchorLeft = (1f - COMPASS_WIDTH_PERCENT) / 2f;
		_compassBar.AnchorRight = 1f - (1f - COMPASS_WIDTH_PERCENT) / 2f;
		_compassBar.AnchorTop = 0;
		_compassBar.AnchorBottom = 1;
		_compassBar.OffsetTop = COMPASS_Y_OFFSET;
		_compassBar.OffsetBottom = -COMPASS_Y_OFFSET;

		// Style the compass bar
		var styleBox = new StyleBoxFlat();
		styleBox.BgColor = new Color(0, 0, 0, 0.6f); // Semi-transparent black
		styleBox.BorderColor = new Color(0.8f, 0.7f, 0.5f, 0.9f); // Gold border
		styleBox.BorderWidthTop = 2;
		styleBox.BorderWidthBottom = 2;
		styleBox.BorderWidthLeft = 2;
		styleBox.BorderWidthRight = 2;
		styleBox.CornerRadiusTopLeft = 5;
		styleBox.CornerRadiusTopRight = 5;
		styleBox.CornerRadiusBottomLeft = 5;
		styleBox.CornerRadiusBottomRight = 5;

		_compassBar.AddThemeStyleboxOverride("panel", styleBox);
		AddChild(_compassBar);

		// Container for compass content (this will hold moving elements)
		_compassContainer = new Control();
		_compassContainer.AnchorLeft = 0;
		_compassContainer.AnchorRight = 1;
		_compassContainer.AnchorTop = 0;
		_compassContainer.AnchorBottom = 1;
		_compassContainer.ClipContents = true; // Important: clips content outside bounds
		_compassBar.AddChild(_compassContainer);

		// Create cardinal direction labels
		CreateCardinalLabels();

		// Create center indicator (shows where camera is pointing)
		CreateCenterIndicator();
	}

	private void CreateCardinalLabels()
	{
		// Create labels for N, NE, E, SE, S, SW, W, NW
		string[] directions = { "N", "NW", "W", "SW", "S", "SE", "E", "NE" };

		for (int i = 0; i < 360; i++)
		{
			var label = new Label();
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.VerticalAlignment = VerticalAlignment.Center;

			if (i % 5 == 0)
			{
				// Style the tick mark
				var indicator = new ColorRect();
				indicator.Color = new Color(1f, 0.9f, 0.6f);
				indicator.CustomMinimumSize = new Vector2(2, 10);

				if (i % 45 == 0)
				{
					label.Text = directions[i / 45];

					// Style the label
					label.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.7f));
					label.AddThemeColorOverride("font_outline_color", Colors.Black);
					label.AddThemeConstantOverride("outline_size", 2);
					label.AddThemeFontSizeOverride("font_size", 18);

					// Make cardinal directions (N, E, S, W) larger
					if (i % 90 == 0)
					{
						label.AddThemeFontSizeOverride("font_size", 24);
					}

					indicator.CustomMinimumSize = new Vector2(2, 5);
				}

				label.AddChild(indicator);
				_compassContainer.AddChild(label);
				_compassLabels[i] = label;
			}
		}
	}

	private void CreateCenterIndicator()
	{
		// Small marker at center showing current direction
		var indicator = new ColorRect();
		indicator.Color = new Color(1f, 0.9f, 0.6f); // Gold color
		indicator.CustomMinimumSize = new Vector2(3, 20);
		indicator.AnchorLeft = 0.5f;
		indicator.AnchorRight = 0.5f;
		indicator.AnchorTop = 0;
		indicator.AnchorBottom = 0;
		indicator.OffsetLeft = -1.5f;
		indicator.OffsetRight = 1.5f;
		indicator.OffsetTop = 5f;
		indicator.OffsetBottom = 5f;

		_compassBar.AddChild(indicator);
	}

	public void UpdatePOIs(Vector3 playerWorldPos, IEnumerable<Vector2> poiWorldXZ)
	{
		if (_camera == null || _compassContainer == null)
			return;

		// Get camera yaw (Y rotation in radians)
		// Note: Godot's Basis.GetEuler() returns rotation in radians
		float cameraYaw = _camera.GlobalTransform.Basis.GetEuler().Y;

		// Update compass direction labels and ticks
		UpdateCompassLabels(cameraYaw);

		// Clear old POI markers
		foreach (var marker in _markers.Values)
			marker.QueueFree();
		_markers.Clear();

		// Create new POI markers
		Vector2 playerXZ = new Vector2(playerWorldPos.X, playerWorldPos.Z);

		foreach (var poi in poiWorldXZ)
		{
			// Calculate angle to POI from player position
			Vector2 toPOI = playerXZ - poi;
			float angleToTarget = Mathf.Atan2(toPOI.X, toPOI.Y); // Angle in world space

			// Calculate relative angle (difference between target angle and camera angle)
			float relativeAngle = angleToTarget - cameraYaw;

			// Normalize angle to -PI to PI range
			relativeAngle = NormalizeAngle(relativeAngle);

			// Only show POIs within ~160 degrees of view (80 degrees on each side)
			float maxDisplayAngle = Mathf.DegToRad(80f);
			if (Mathf.Abs(relativeAngle) > maxDisplayAngle)
				continue; // POI is too far to the side, don't show

			// Convert angle to position on compass bar
			// relativeAngle 0 = center, -PI = left edge, +PI = right edge
			float normalizedPosition = relativeAngle / maxDisplayAngle; // -1 to 1

			// Create and position marker
			var marker = CreatePOIMarker(poi, playerXZ);
			_compassContainer.AddChild(marker);

			// Position marker (0.5 = center of bar)
			float xPosition = 0.5f + (normalizedPosition * 0.5f);
			marker.AnchorLeft = xPosition;
			marker.AnchorRight = xPosition;
			marker.AnchorTop = 0.5f;
			marker.AnchorBottom = 0.5f;
			marker.OffsetLeft = -MARKER_SIZE / 2f;
			marker.OffsetRight = MARKER_SIZE / 2f;
			marker.OffsetTop = -MARKER_SIZE / 2f;
			marker.OffsetBottom = MARKER_SIZE / 2f;

			_markers[poi] = marker;
		}
	}

	private void UpdateCompassLabels(float cameraYaw)
	{
		float compassWidth = _compassContainer.Size.X;
		float maxDisplayAngle = Mathf.DegToRad(80f); // Same as POI display range

		foreach (var kvp in _compassLabels)
		{
			float cardinalAngle = Mathf.DegToRad(kvp.Key);
			Label label = kvp.Value;

			// Calculate relative angle to this cardinal direction
			float relativeAngle = cardinalAngle - cameraYaw;
			relativeAngle = NormalizeAngle(relativeAngle);

			// Check if this direction is visible
			if (Mathf.Abs(relativeAngle) > maxDisplayAngle)
			{
				label.Visible = false;
				continue;
			}

			label.Visible = true;

			// Position label
			float normalizedPosition = relativeAngle / maxDisplayAngle;
			float xPosition = 0.5f + (normalizedPosition * 0.5f);

			label.AnchorLeft = xPosition;
			label.AnchorRight = xPosition;
			label.AnchorTop = 0.5f;
			label.AnchorBottom = 0.5f;
			label.OffsetLeft = -30; // Half of label width estimate
			label.OffsetRight = 30;
			label.OffsetTop = -15;
			label.OffsetBottom = 15;
		}
	}

	private Control CreatePOIMarker(Vector2 poiPos, Vector2 playerPos)
	{
		// Calculate distance to POI
		float distance = playerPos.DistanceTo(poiPos);

		// Create container for marker + distance label
		var container = new VBoxContainer();
		container.AddThemeConstantOverride("separation", 2);

		// Icon (you can customize this based on POI type)
		var icon = new ColorRect();
		icon.Color = new Color(1f, 0.8f, 0.2f); // Gold color
		icon.CustomMinimumSize = new Vector2(MARKER_SIZE, MARKER_SIZE);
		container.AddChild(icon);

		// Distance label
		var distLabel = new Label();
		distLabel.Text = FormatDistance(distance);
		distLabel.HorizontalAlignment = HorizontalAlignment.Center;
		distLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
		distLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		distLabel.AddThemeConstantOverride("outline_size", 1);
		distLabel.AddThemeFontSizeOverride("font_size", 10);
		container.AddChild(distLabel);

		return container;
	}

	private string FormatDistance(float distance)
	{
		if (distance < 100f)
			return $"{distance:F0}m";
		else if (distance < 1000f)
			return $"{distance:F0}m";
		else
			return $"{distance / 1000f:F1}km";
	}

	private float NormalizeAngle(float angle)
	{
		// Normalize angle to -PI to PI range
		while (angle > Mathf.Pi)
			angle -= Mathf.Tau;
		while (angle < -Mathf.Pi)
			angle += Mathf.Tau;
		return angle;
	}
}