using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

public partial class Compass : Control
{
	private Panel _compassBar;
	private Control _compassContainer;
	private Camera3D _camera;

	// Cardinal direction labels (N, E, S, W)
	private Dictionary<float, Label> _compassLabels = new();

	// POI markers
	private Dictionary<Vector2, Control> _markers = new();

	private Panel _leftFade;
	private Panel _rightFade;

	// Compass settings
	private const float COMPASS_WIDTH_PERCENT = 0.8f; // 80% of screen width
	private const float COMPASS_HEIGHT = 40f;
	private const float COMPASS_Y_OFFSET = 10f;
	private const float MARKER_SIZE = 8f;
	private const float FADE_WIDTH = 60f;

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
		_compassContainer.OffsetLeft = 5;
		_compassContainer.OffsetRight = -5;
		_compassContainer.OffsetTop = 0;
		_compassContainer.OffsetBottom = 0;
		_compassContainer.ClipContents = true; // Important: clips content outside bounds
		_compassBar.AddChild(_compassContainer);

		// Create cardinal direction labels
		CreateCardinalLabels();

		// Create center indicator (shows where camera is pointing)
		CreateCenterIndicator();

		//CreateFadeEdges();
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
				indicator.OffsetLeft = 28; // Position tick mark at center of label (assuming label width ~60)
				indicator.OffsetRight = -28;

				if (i % 45 == 0)
				{
					label.Text = directions[i / 45];

					// Style the label
					label.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.7f));
					label.AddThemeColorOverride("font_outline_color", Colors.Black);
					label.AddThemeConstantOverride("outline_size", 2);
					label.AddThemeFontSizeOverride("font_size", 18);
					//label.OffsetRight = -5;
					indicator.CustomMinimumSize = new Vector2(2, 5);
					// Make cardinal directions (N, E, S, W) larger
					if (i % 90 == 0)
					{
						label.AddThemeFontSizeOverride("font_size", 24);
					}
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

	private void CreateFadeEdges()
	{
		// Left fade
		_leftFade = new Panel();
		_leftFade.AnchorLeft = 0;
		_leftFade.AnchorRight = 0;
		_leftFade.AnchorTop = 0;
		_leftFade.AnchorBottom = 1;
		_leftFade.OffsetRight = FADE_WIDTH;
		_leftFade.MouseFilter = MouseFilterEnum.Ignore; // Don't block mouse events

		// Create gradient style - fades from opaque black to transparent
		var leftGradient = new StyleBoxFlat();
		leftGradient.BgColor = new Color(0, 0, 0, 0.6f); // Match compass background
		leftGradient.CornerRadiusTopLeft = 5;
		leftGradient.CornerRadiusBottomLeft = 5;

		// LEARNING NOTE: Godot 4 doesn't support gradient in StyleBoxFlat directly,
		// so we'll use a ColorRect with a Gradient texture instead
		var leftGradientRect = new ColorRect();
		leftGradientRect.AnchorLeft = 0;
		leftGradientRect.AnchorRight = 0;
		leftGradientRect.AnchorTop = 0;
		leftGradientRect.AnchorBottom = 1;
		leftGradientRect.OffsetRight = FADE_WIDTH;
		leftGradientRect.MouseFilter = MouseFilterEnum.Ignore;

		// Create gradient texture
		var leftGradientTexture = new GradientTexture2D();
		var leftGradientData = new Gradient();
		leftGradientData.SetColor(0, new Color(0, 0, 0, 0.6f)); // Left edge - opaque
		leftGradientData.SetColor(1, new Color(0, 0, 0, 0));    // Right edge - transparent
		leftGradientTexture.Gradient = leftGradientData;
		leftGradientTexture.Width = (int)FADE_WIDTH;
		leftGradientTexture.Height = (int)COMPASS_HEIGHT;
		leftGradientTexture.Fill = GradientTexture2D.FillEnum.Linear;
		leftGradientTexture.FillFrom = new Vector2(0, 0.5f);
		leftGradientTexture.FillTo = new Vector2(1, 0.5f);

		leftGradientRect.DrawTexture(leftGradientTexture, new Vector2(0, 0.5f));
		_compassBar.AddChild(leftGradientRect);

		// Right fade (mirror of left)
		var rightGradientRect = new ColorRect();
		rightGradientRect.AnchorLeft = 1;
		rightGradientRect.AnchorRight = 1;
		rightGradientRect.AnchorTop = 0;
		rightGradientRect.AnchorBottom = 1;
		rightGradientRect.OffsetLeft = -FADE_WIDTH;
		rightGradientRect.MouseFilter = MouseFilterEnum.Ignore;

		var rightGradientTexture = new GradientTexture2D();
		var rightGradientData = new Gradient();
		rightGradientData.SetColor(0, new Color(0, 0, 0, 0));    // Left edge - transparent
		rightGradientData.SetColor(1, new Color(0, 0, 0, 0.6f)); // Right edge - opaque
		rightGradientTexture.Gradient = rightGradientData;
		rightGradientTexture.Width = (int)FADE_WIDTH;
		rightGradientTexture.Height = (int)COMPASS_HEIGHT;
		rightGradientTexture.Fill = GradientTexture2D.FillEnum.Linear;
		rightGradientTexture.FillFrom = new Vector2(0, 0.5f);
		rightGradientTexture.FillTo = new Vector2(1, 0.5f);

		rightGradientRect.DrawTexture(rightGradientTexture, new Vector2(0, 0.5f));
		_compassBar.AddChild(rightGradientRect);
	}

	public void UpdatePOIs(Vector3 playerWorldPos, IEnumerable<Vector2> poiWorldXZ, IEnumerable<string> poiTypes)
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

		for (int i = 0; i < poiWorldXZ.Count(); i++)
		{
			// Calculate angle to POI from player position
			Vector2 toPOI = playerXZ - poiWorldXZ.ElementAt(i);
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
			var marker = CreatePOIMarker(playerXZ, poiWorldXZ.ElementAt(i), poiTypes.ElementAt(i));
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

			_markers[poiWorldXZ.ElementAt(i)] = marker;
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
			label.OffsetLeft = -30;
			label.OffsetRight = 30;
			label.OffsetTop = -15;
			label.OffsetBottom = 15;
		}
	}

	private Control CreatePOIMarker(Vector2 playerPos, Vector2 poiPos, string poiType)
	{
		// Calculate distance to POI
		float distance = playerPos.DistanceTo(poiPos);

		// Create container for marker + distance label
		var container = new VBoxContainer();
		container.AddThemeConstantOverride("separation", 2);

		// Icon (you can customize this based on POI type)
		var icon = new ColorRect();
		switch (poiType)
		{
			case "dungeon":
				icon.Color = new Color(0.26f, 0.17f, 0.84f); // Cyan for resources
				break;
			case "boss":
				icon.Color = new Color(0.53f, 0.03f, 0.03f); // Red for enemies
				break;
			default:
				icon.Color = new Color(0.11f, 0.33f, 0.19f); // Green color
				break;
		}
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