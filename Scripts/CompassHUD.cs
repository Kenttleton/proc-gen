using Godot;
using System;
using System.Collections.Generic;

public partial class Compass : CanvasLayer
{
	private Control _root;
	private Control _compassBar;
	private Label _northLabel;
	private Camera3D _camera;

	private readonly Dictionary<Vector2, Control> _markers = new();

	public override void _Ready()
	{
		BuildCompass();
	}

	public Compass(Control root, Camera3D camera)
	{
		_camera = camera;
		_root = root;
	}

	private void BuildCompass()
	{
		_compassBar = new Panel();
		_compassBar.AnchorLeft = 0.1f;
		_compassBar.AnchorRight = 0.9f;
		_compassBar.AnchorTop = 0;
		_compassBar.AnchorBottom = 1;
		_compassBar.CustomMinimumSize = new Vector2(0, 60);
		_compassBar.Position = new Vector2(0, 10);
		_root.AddChild(_compassBar);

		_northLabel = new Label();
		_northLabel.Text = "N";
		_northLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_northLabel.AnchorLeft = 0;
		_northLabel.AnchorRight = 1;
		_northLabel.AnchorTop = 0;
		_northLabel.AnchorBottom = 1;
		_northLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_compassBar.AddChild(_northLabel);
	}

	public void UpdatePOIs(Vector3 playerWorldPos, IEnumerable<Vector2> poiWorldXZ)
	{
		if (_camera == null)
			return;

		float yaw = _camera.GlobalTransform.Basis.GetEuler().Y;

		foreach (var child in _markers.Values)
			child.QueueFree();

		_markers.Clear();

		foreach (var poi in poiWorldXZ)
		{
			var marker = CreateMarker();
			_compassBar.AddChild(marker);

			Vector2 playerXZ = new(playerWorldPos.X, playerWorldPos.Z);
			Vector2 toPOI = poi - playerXZ;
			float angle = Mathf.Atan2(toPOI.X, toPOI.Y);

			float relativeAngle = angle - yaw;
			float normalized = Mathf.Wrap(relativeAngle / Mathf.Pi, -1, 1);

			float barWidth = _compassBar.Size.X;
			float x = barWidth * 0.5f + normalized * barWidth * 0.5f;

			marker.Position = new Vector2(x, _compassBar.Size.Y * 0.5f - 10);
		}
	}

	private Control CreateMarker()
	{
		var marker = new ColorRect();
		marker.Color = new Color(1, 0.8f, 0.2f);
		marker.CustomMinimumSize = new Vector2(6, 20);
		marker.AnchorTop = 0.5f;
		marker.AnchorBottom = 0.5f;
		return marker;
	}
}

