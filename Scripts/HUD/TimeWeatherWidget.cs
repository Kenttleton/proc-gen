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
    public DayNightCycleManager _dayNightManager;
    public ShaderWeatherSystem _weatherSystem;

    // UI Elements
    private TimeCircleDrawer _circleDrawer;
    private Label _timeLabel;
    private Label _weatherLabel;
    private Label _temperatureLabel;

    // Visual settings
    private const float WIDGET_SIZE = 120f;

    // Time markers (for drawing)
    public float _sunriseAngle = 0f;  // Calculated angle for sunrise
    public float _sunsetAngle = 0f;   // Calculated angle for sunset
    public float _currentTimeAngle = 0f; // Current time on the circle
    public bool _isDaytime = false;

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
        _circleDrawer = new TimeCircleDrawer(this);
        _circleDrawer.AnchorLeft = 0;
        _circleDrawer.AnchorRight = 1;
        _circleDrawer.AnchorTop = 0;
        _circleDrawer.AnchorBottom = 0;
        _circleDrawer.OffsetBottom = WIDGET_SIZE * 0.65f; // Top portion for circle
        AddChild(_circleDrawer);

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
        _circleDrawer.AddChild(_timeLabel);

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
        _circleDrawer?.QueueRedraw();
    }

    private void UpdateTimeDisplay()
    {
        // Get current time from day/night manager
        string timeString = _dayNightManager.GetTimeString();
        _timeLabel.Text = timeString;

        // Calculate angles for visualization
        float timeOfDay = _dayNightManager.TimeOfDay;

        _isDaytime = timeOfDay >= 0.25f && timeOfDay <= 0.75f;

        // Current time angle (rotate the half-circle based on time)
        // 0.0 (midnight) = 180° (bottom), 0.5 (noon) = 0° (top), 1.0 (midnight) = 180° (bottom)
        // _currentTimeAngle = (timeOfDay - 0.5f) * Mathf.Pi; // -90° to +90°

        // Sunrise at 6 AM (0.25) and sunset at 6 PM (0.75)
        // These are fixed positions on our rotating half-circle
        float sunriseTime = 0.25f;  // 6 AM
        float sunsetTime = 0.75f;   // 6 PM

        if (_isDaytime)
        {
            // DAYTIME: Show sun's arc from sunrise to sunset
            // Map sunrise/sunset to arc positions
            _sunriseAngle = MapTimeToArcAngle(sunriseTime, timeOfDay, true);
            _sunsetAngle = MapTimeToArcAngle(sunsetTime, timeOfDay, true);
        }
        else
        {
            // NIGHTTIME: Show moon's arc
            // Moon rises when sun sets, sets when sun rises
            _sunriseAngle = MapTimeToArcAngle(sunriseTime, timeOfDay, false);
            _sunsetAngle = MapTimeToArcAngle(sunsetTime, timeOfDay, false);
        }
    }

    /// <summary>
	/// Maps a time value to an angle on the arc.
	/// FIX: Proper rotation - top of arc (angle 0) = current time
	/// </summary>
	private float MapTimeToArcAngle(float targetTime, float currentTime, bool isDaytime)
    {
        // Calculate time difference
        float timeDiff = targetTime - currentTime;

        // Handle wraparound (e.g., if current is 11 PM and target is 1 AM)
        if (timeDiff > 0.5f)
            timeDiff -= 1.0f;
        else if (timeDiff < -0.5f)
            timeDiff += 1.0f;

        // FIX: Map to arc angle
        // The arc spans 12 hours (0.5 of day cycle)
        // -PI/2 = 6 hours ago (left)
        // 0 = now (top)
        // +PI/2 = 6 hours from now (right)

        // timeDiff is in range [-0.5, 0.5] (fraction of day)
        // Convert to radians: multiply by 2*PI (full circle) = Tau
        float angle = timeDiff * Mathf.Tau;

        return angle;
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
}