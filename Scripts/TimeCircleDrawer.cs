using Godot;
public partial class TimeCircleDrawer : Control
{
    private TimeWeatherWidget _parent;
    private const float CIRCLE_RADIUS = 50f;
    private const float SUN_MARKER_SIZE = 8f;

    public TimeCircleDrawer(TimeWeatherWidget parent)
    {
        _parent = parent;
    }

    public override void _Draw()
    {
        if (_parent?._dayNightManager == null)
            return;

        Vector2 center = Size / 2f;
        center.Y = CIRCLE_RADIUS + 10;

        if (_parent._isDaytime)
            DrawDaytimeGradient(center);
        else
            DrawNighttimeGradient(center);

        if (_parent._isDaytime)
        {
            DrawSunMarker(center, _parent._sunriseAngle, true);   // Sunrise marker
            DrawSunMarker(center, _parent._sunsetAngle, false);   // Sunset marker
        }
        else
        {
            DrawMoonMarker(center, _parent._sunriseAngle, true);  // Moonset (at sunrise)
            DrawMoonMarker(center, _parent._sunsetAngle, false);  // Moonrise (at sunset)
        }
        DrawCurrentTimeIndicator(center);
    }

    /// <summary>
    /// FIX: Daytime gradient - sun arc from sunrise to sunset
    /// Orange (sunrise) → Yellow → Blue (noon) → Yellow → Orange (sunset)
    /// </summary>
    private void DrawDaytimeGradient(Vector2 center)
    {
        int segments = 32;
        float angleStep = Mathf.Pi / segments;

        for (int i = 0; i < segments; i++)
        {
            // FIX: Arc goes from left (-PI/2) to right (+PI/2) with top (0) = now
            float startAngle = -Mathf.Pi / 2f + (i * angleStep);
            float endAngle = startAngle + angleStep;

            // t goes from 0 (left/morning) to 1 (right/evening)
            float t = (float)i / segments;
            Color color = GetDaytimeColor(t);

            Vector2 start = center + new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * CIRCLE_RADIUS;
            Vector2 end = center + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * CIRCLE_RADIUS;

            DrawLine(start, end, color, 4f);
        }

        // Base line
        Vector2 leftEnd = center + new Vector2(-CIRCLE_RADIUS, 0);
        Vector2 rightEnd = center + new Vector2(CIRCLE_RADIUS, 0);
        DrawLine(leftEnd, rightEnd, new Color(0.8f, 0.7f, 0.5f), 2f);
    }

    /// <summary>
    /// FIX: Nighttime gradient - moon arc through twilight
    /// Dark blue throughout, subtle gradient for twilight periods
    /// </summary>
    private void DrawNighttimeGradient(Vector2 center)
    {
        int segments = 32;
        float angleStep = Mathf.Pi / segments;

        for (int i = 0; i < segments; i++)
        {
            float startAngle = -Mathf.Pi / 2f + (i * angleStep);
            float endAngle = startAngle + angleStep;

            float t = (float)i / segments;
            Color color = GetNighttimeColor(t);

            Vector2 start = center + new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * CIRCLE_RADIUS;
            Vector2 end = center + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * CIRCLE_RADIUS;

            DrawLine(start, end, color, 4f);
        }

        // Base line
        Vector2 leftEnd = center + new Vector2(-CIRCLE_RADIUS, 0);
        Vector2 rightEnd = center + new Vector2(CIRCLE_RADIUS, 0);
        DrawLine(leftEnd, rightEnd, new Color(0.3f, 0.3f, 0.4f), 2f);
    }

    private Color GetDaytimeColor(float t)
    {
        // Sun arc colors: sunrise → morning → noon → evening → sunset

        if (t < 0.25f)
        {
            // Early morning: orange → yellow
            float blend = t / 0.25f;
            return new Color(1f, 0.5f, 0.2f).Lerp(new Color(1f, 0.9f, 0.4f), blend);
        }
        else if (t < 0.5f)
        {
            // Morning to noon: yellow → bright blue
            float blend = (t - 0.25f) / 0.25f;
            return new Color(1f, 0.9f, 0.4f).Lerp(new Color(0.4f, 0.7f, 1f), blend);
        }
        else if (t < 0.75f)
        {
            // Noon to evening: bright blue → yellow
            float blend = (t - 0.5f) / 0.25f;
            return new Color(0.4f, 0.7f, 1f).Lerp(new Color(1f, 0.9f, 0.4f), blend);
        }
        else
        {
            // Evening to sunset: yellow → orange
            float blend = (t - 0.75f) / 0.25f;
            return new Color(1f, 0.9f, 0.4f).Lerp(new Color(1f, 0.5f, 0.2f), blend);
        }
    }

    private Color GetNighttimeColor(float t)
    {
        // Night arc colors: dark throughout, subtle variations for twilight

        if (t < 0.15f || t > 0.85f)
        {
            // Near dusk/dawn: slightly lighter (twilight)
            return new Color(0.1f, 0.1f, 0.2f);
        }
        else
        {
            // Deep night: darkest
            return new Color(0.05f, 0.05f, 0.15f);
        }
    }

    private void DrawSunMarker(Vector2 center, float angle, bool isSunrise)
    {
        // Only draw if marker is on the visible half-circle
        if (angle < -Mathf.Pi / 2f || angle > Mathf.Pi / 2f)
            return;

        Vector2 markerPos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * CIRCLE_RADIUS;

        Color markerColor = isSunrise
            ? new Color(1f, 0.8f, 0.2f)  // Sunrise: bright yellow
            : new Color(1f, 0.4f, 0.1f); // Sunset: orange-red

        DrawCircle(markerPos, SUN_MARKER_SIZE, markerColor);
        DrawArc(markerPos, SUN_MARKER_SIZE, 0, Mathf.Tau, 16, Colors.Black, 1.5f);

        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 lineEnd = markerPos + direction * 12f;
        DrawLine(markerPos, lineEnd, markerColor, 2f);
    }

    private void DrawMoonMarker(Vector2 center, float angle, bool isMoonset)
    {
        if (angle < -Mathf.Pi / 2f || angle > Mathf.Pi / 2f)
            return;

        Vector2 markerPos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * CIRCLE_RADIUS;

        // Moon is light blue/white
        Color markerColor = new Color(0.9f, 0.9f, 1.0f);

        DrawCircle(markerPos, SUN_MARKER_SIZE, markerColor);
        DrawArc(markerPos, SUN_MARKER_SIZE, 0, Mathf.Tau, 16, new Color(0.5f, 0.5f, 0.7f), 1.5f);

        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 lineEnd = markerPos + direction * 12f;
        DrawLine(markerPos, lineEnd, markerColor, 2f);
    }

    private void DrawCurrentTimeIndicator(Vector2 center)
    {
        Vector2 topPoint = center + new Vector2(0, -CIRCLE_RADIUS - 8);
        Vector2 arrowBase = center + new Vector2(0, -CIRCLE_RADIUS);

        Vector2[] triangle = {
                topPoint,
                arrowBase + new Vector2(-4, 0),
                arrowBase + new Vector2(4, 0)
            };

        DrawColoredPolygon(triangle, new Color(1f, 1f, 1f, 0.9f));

        // Draw outline
        Vector2[] outline = { triangle[0], triangle[1], triangle[2], triangle[0] };
        DrawPolyline(outline, Colors.Black, 1.5f);
    }
}