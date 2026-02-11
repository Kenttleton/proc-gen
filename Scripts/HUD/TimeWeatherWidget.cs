using Godot;
using System;

/// <summary>
/// Displays a circular time/weather widget showing:
/// - Rotating half-circle with sunrise/sunset markers
/// - Current time in the center
/// - Weather icon
/// - Temperature
/// </summary>
public partial class TimeWeatherWidget : Control
{
    private DayNightCycleManager _dayNightManager;
    private ShaderWeatherSystem _weatherSystem;

    // UI Elements
    private Control _circleContainer;
    private Label _timeLabel;
    private Label _weatherLabel;
    private Label _temperatureLabel;

    // Visual settings
    private const float WIDGET_SIZE = 120f;
    private const float CIRCLE_RADIUS = 50f;
    private const float SUN_MARKER_SIZE = 8f;

    // Time markers (for drawing)
    private float _sunriseAngle = 0f;  // Calculated angle for sunrise
    private float _sunsetAngle = 0f;   // Calculated angle for sunset
    private float _currentTimeAngle = 0f; // Current time on the circle

    public TimeWeatherWidget(DayNightCycleManager dayNightManager, ShaderWeatherSystem weatherSystem)
    {
        _dayNightManager = dayNightManager;
        _weatherSystem = weatherSystem;
        Name = "TimeWeatherWidget";
    }

    public override void _Ready()
    {
        // Position widget in bottom-left corner
        AnchorLeft = 0;
        AnchorRight = 0;
        AnchorTop = 1;
        AnchorBottom = 1;
        OffsetLeft = 20;
        OffsetTop = -(WIDGET_SIZE + 20);
        OffsetRight = WIDGET_SIZE + 20;
        OffsetBottom = -20;

        BuildWidget();
    }

    private void BuildWidget()
    {
        // Background panel
        var background = new Panel();
        background.AnchorLeft = 0;
        background.AnchorRight = 1;
        background.AnchorTop = 0;
        background.AnchorBottom = 1;

        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0, 0, 0, 0.7f);
        bgStyle.BorderColor = new Color(0.8f, 0.7f, 0.5f, 0.9f);
        bgStyle.SetBorderWidthAll(2);
        bgStyle.SetCornerRadiusAll(10);
        background.AddThemeStyleboxOverride("panel", bgStyle);
        AddChild(background);

        // Container for the circular drawing
        _circleContainer = new Control();
        _circleContainer.AnchorLeft = 0;
        _circleContainer.AnchorRight = 1;
        _circleContainer.AnchorTop = 0;
        _circleContainer.AnchorBottom = 0;
        _circleContainer.OffsetBottom = WIDGET_SIZE * 0.65f; // Top portion for circle
        AddChild(_circleContainer);

        // Time label (center of circle)
        _timeLabel = new Label();
        _timeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _timeLabel.VerticalAlignment = VerticalAlignment.Center;
        _timeLabel.AnchorLeft = 0;
        _timeLabel.AnchorRight = 1;
        _timeLabel.AnchorTop = 0.3f;
        _timeLabel.AnchorBottom = 0.5f;
        _timeLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.95f));
        _timeLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _timeLabel.AddThemeConstantOverride("outline_size", 2);
        _timeLabel.AddThemeFontSizeOverride("font_size", 18);
        _circleContainer.AddChild(_timeLabel);

        // Weather and temperature info (bottom portion)
        var infoContainer = new HBoxContainer();
        infoContainer.AnchorLeft = 0;
        infoContainer.AnchorRight = 1;
        infoContainer.AnchorTop = 0.65f;
        infoContainer.AnchorBottom = 1;
        infoContainer.AddThemeConstantOverride("separation", 10);
        infoContainer.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(infoContainer);

        // Weather icon label
        _weatherLabel = new Label();
        _weatherLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _weatherLabel.VerticalAlignment = VerticalAlignment.Center;
        _weatherLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.7f));
        _weatherLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _weatherLabel.AddThemeConstantOverride("outline_size", 1);
        _weatherLabel.AddThemeFontSizeOverride("font_size", 20);
        infoContainer.AddChild(_weatherLabel);

        // Temperature label
        _temperatureLabel = new Label();
        _temperatureLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _temperatureLabel.VerticalAlignment = VerticalAlignment.Center;
        _temperatureLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.7f));
        _temperatureLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _temperatureLabel.AddThemeConstantOverride("outline_size", 1);
        _temperatureLabel.AddThemeFontSizeOverride("font_size", 14);
        infoContainer.AddChild(_temperatureLabel);
    }

    public override void _Process(double delta)
    {
        if (_dayNightManager == null)
            return;

        UpdateTimeDisplay();
        UpdateWeatherDisplay();

        // Trigger redraw for the rotating circle
        _circleContainer.QueueRedraw();
    }

    private void UpdateTimeDisplay()
    {
        // Get current time from day/night manager
        string timeString = _dayNightManager.GetTimeString();
        _timeLabel.Text = timeString;

        // Calculate angles for visualization
        float timeOfDay = _dayNightManager.TimeOfDay;

        // Current time angle (rotate the half-circle based on time)
        // 0.0 (midnight) = 180° (bottom), 0.5 (noon) = 0° (top), 1.0 (midnight) = 180° (bottom)
        _currentTimeAngle = (timeOfDay - 0.5f) * Mathf.Pi; // -90° to +90°

        // Sunrise at 6 AM (0.25) and sunset at 6 PM (0.75)
        // These are fixed positions on our rotating half-circle
        float sunriseTime = 0.25f; // 6 AM
        float sunsetTime = 0.75f;  // 6 PM

        // Calculate relative angles from current time
        _sunriseAngle = (sunriseTime - timeOfDay) * Mathf.Tau;
        _sunsetAngle = (sunsetTime - timeOfDay) * Mathf.Tau;
    }

    private void UpdateWeatherDisplay()
    {
        if (_weatherSystem == null)
        {
            _weatherLabel.Text = "☀";
            _temperatureLabel.Text = "20°C";
            return;
        }

        // Get weather icon based on current weather
        // You'll need to track current weather type in your ShaderWeatherSystem
        // For now, we'll determine it from rain/fog intensity
        string weatherIcon = GetWeatherIcon();
        _weatherLabel.Text = weatherIcon;

        // Calculate temperature based on time and weather
        // LEARNING NOTE: This is a simple simulation. In a real game, you'd have
        // a proper temperature system based on biome, altitude, time, and weather.
        float temperature = CalculateTemperature();
        _temperatureLabel.Text = $"{temperature:F0}°C";
    }

    private string GetWeatherIcon()
    {
        // Simple weather icon determination
        // You can expand this based on actual weather states
        float weatherDarkness = _weatherSystem?.WeatherDarkness ?? 0f;

        if (weatherDarkness > 0.5f)
            return "⛈"; // Stormy
        else if (weatherDarkness > 0.3f)
            return "🌧"; // Rainy
        else if (weatherDarkness > 0.1f)
            return "☁"; // Cloudy
        else if (_dayNightManager.IsNight)
            return "🌙"; // Clear night
        else
            return "☀"; // Clear day
    }

    private float CalculateTemperature()
    {
        // Simulate temperature based on time of day
        float timeOfDay = _dayNightManager.TimeOfDay;

        // Base temperature curve (coldest at night, warmest at 2 PM)
        // Peak warmth at 0.58 (2 PM), coldest at 0.08 (2 AM)
        float timeTemp = Mathf.Sin((timeOfDay - 0.08f) * Mathf.Tau) * 8f + 18f; // 10°C to 26°C range

        // Weather affects temperature (storms cool things down)
        float weatherDarkness = _weatherSystem?.WeatherDarkness ?? 0f;
        float weatherEffect = -weatherDarkness * 5f; // Up to -5°C in storms

        return Mathf.Clamp(timeTemp + weatherEffect, 0f, 40f);
    }

    // LEARNING NOTE: This is where we custom-draw the rotating half-circle!
    // Godot calls _Draw() automatically when QueueRedraw() is called.
    public override void _Draw()
    {
        if (_circleContainer == null)
            return;

        // We'll draw on the _circleContainer, so we need to override its draw
        // Actually, we need to connect to its draw signal
        // Let me use a different approach - draw directly here and position correctly
    }

    // Better approach: Use a custom Control node for drawing
    // Let me add this as a nested class
    public override void _EnterTree()
    {
        base._EnterTree();

        if (_circleContainer != null)
        {
            _circleContainer.Draw += DrawTimeCircle;
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        if (_circleContainer != null)
        {
            _circleContainer.Draw -= DrawTimeCircle;
        }
    }

    private void DrawTimeCircle()
    {
        if (_dayNightManager == null || _circleContainer == null)
            return;

        Vector2 center = _circleContainer.Size / 2f;
        center.Y = CIRCLE_RADIUS + 10; // Position circle in upper portion

        // Draw the half-circle background (day/night gradient)
        DrawDayNightGradient(center);

        // Draw sunrise and sunset markers
        DrawSunMarker(center, _sunriseAngle, true);  // Sunrise
        DrawSunMarker(center, _sunsetAngle, false);  // Sunset

        // Draw the current time indicator (small line at top)
        DrawCurrentTimeIndicator(center);
    }

    private void DrawDayNightGradient(Vector2 center)
    {
        // Draw a half-circle with gradient from sunrise (orange) to midday (blue) to sunset (orange)
        // We'll draw multiple arc segments with different colors

        int segments = 32;
        float angleStep = Mathf.Pi / segments;

        for (int i = 0; i < segments; i++)
        {
            float startAngle = -Mathf.Pi / 2f + (i * angleStep);
            float endAngle = startAngle + angleStep;

            // Calculate color based on position on the arc
            // -PI/2 (left/sunrise) = orange, 0 (top/noon) = blue, PI/2 (right/sunset) = orange
            float t = (float)i / segments; // 0 to 1
            Color color = GetTimeColor(t);

            // Draw arc segment
            Vector2 start = center + new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * CIRCLE_RADIUS;
            Vector2 end = center + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * CIRCLE_RADIUS;

            _circleContainer.DrawLine(start, end, color, 4f);
        }

        // Draw the base line of the half-circle
        Vector2 leftEnd = center + new Vector2(-CIRCLE_RADIUS, 0);
        Vector2 rightEnd = center + new Vector2(CIRCLE_RADIUS, 0);
        _circleContainer.DrawLine(leftEnd, rightEnd, new Color(0.5f, 0.5f, 0.5f), 2f);
    }

    private Color GetTimeColor(float t)
    {
        // t goes from 0 (sunrise/left) to 1 (sunset/right)
        // Create gradient: orange -> yellow -> blue -> yellow -> orange

        if (t < 0.25f)
        {
            // Sunrise to morning
            float blend = t / 0.25f;
            return new Color(1f, 0.5f, 0.2f).Lerp(new Color(1f, 0.9f, 0.4f), blend);
        }
        else if (t < 0.5f)
        {
            // Morning to noon
            float blend = (t - 0.25f) / 0.25f;
            return new Color(1f, 0.9f, 0.4f).Lerp(new Color(0.4f, 0.7f, 1f), blend);
        }
        else if (t < 0.75f)
        {
            // Noon to evening
            float blend = (t - 0.5f) / 0.25f;
            return new Color(0.4f, 0.7f, 1f).Lerp(new Color(1f, 0.9f, 0.4f), blend);
        }
        else
        {
            // Evening to sunset
            float blend = (t - 0.75f) / 0.25f;
            return new Color(1f, 0.9f, 0.4f).Lerp(new Color(1f, 0.5f, 0.2f), blend);
        }
    }

    private void DrawSunMarker(Vector2 center, float angle, bool isSunrise)
    {
        // Only draw if marker is on the visible half-circle (-PI/2 to PI/2)
        if (angle < -Mathf.Pi / 2f || angle > Mathf.Pi / 2f)
            return;

        // Position on the circle
        Vector2 markerPos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * CIRCLE_RADIUS;

        // Marker color - orange/yellow for sunrise, red/orange for sunset
        Color markerColor = isSunrise
            ? new Color(1f, 0.8f, 0.2f)  // Sunrise: bright yellow
            : new Color(1f, 0.4f, 0.1f); // Sunset: orange-red

        // Draw marker as a small circle
        _circleContainer.DrawCircle(markerPos, SUN_MARKER_SIZE, markerColor);
        _circleContainer.DrawArc(markerPos, SUN_MARKER_SIZE, 0, Mathf.Tau, 16, Colors.Black, 1.5f);

        // Draw small line pointing outward to emphasize direction
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 lineEnd = markerPos + direction * 12f;
        _circleContainer.DrawLine(markerPos, lineEnd, markerColor, 2f);
    }

    private void DrawCurrentTimeIndicator(Vector2 center)
    {
        // Draw a small arrow or line at the top of the circle showing "current time"
        Vector2 topPoint = center + new Vector2(0, -CIRCLE_RADIUS - 8);
        Vector2 arrowBase = center + new Vector2(0, -CIRCLE_RADIUS);

        // Draw downward-pointing triangle
        Vector2[] triangle = {
            topPoint,
            arrowBase + new Vector2(-4, 0),
            arrowBase + new Vector2(4, 0)
        };

        _circleContainer.DrawColoredPolygon(triangle, new Color(1f, 1f, 1f, 0.9f));
        _circleContainer.DrawPolyline(triangle, Colors.Black, 1f);
    }
}