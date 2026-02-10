using Godot;
using System;

public enum WeatherType
{
	Clear,
	Rainy,
	Foggy,
	Stormy,
	Cloudy
}

public class WeatherZone
{
	public Vector2 Center; // 2D position in world
	public float Radius;
	public WeatherType Weather;
	public bool IsPermanent; // If false, weather changes over time

	public WeatherZone(Vector2 center, float radius, WeatherType weather, bool isPermanent = false)
	{
		Center = center;
		Radius = radius;
		Weather = weather;
		IsPermanent = isPermanent;
	}

	public bool Contains(Vector3 worldPosition)
	{
		Vector2 pos2D = new Vector2(worldPosition.X, worldPosition.Z);
		return pos2D.DistanceTo(Center) <= Radius;
	}

	public float GetInfluence(Vector3 worldPosition)
	{
		Vector2 pos2D = new Vector2(worldPosition.X, worldPosition.Z);
		float distance = pos2D.DistanceTo(Center);

		if (distance >= Radius)
			return 0.0f;

		// Smooth falloff at edges
		float influence = 1.0f - (distance / Radius);
		return Mathf.SmoothStep(0.0f, 1.0f, influence);
	}
}