using Godot;
using System;

public partial class HeightmapTerrainGenerator : Node3D
{
	[Export] public Image HeightmapImage;
	[Export] public float HeightScale = 50.0f;

	private void GenerateFromHeightmap()
	{
		int width = HeightmapImage.GetWidth();
		int height = HeightmapImage.GetHeight();

		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		for (int z = 0; z < height; z++)
		{
			for (int x = 0; x < width; x++)
			{
				Color pixel = HeightmapImage.GetPixel(x, z);
				float heightValue = pixel.R * HeightScale; // Use red channel

				Vector3 vertex = new Vector3(x, heightValue, z);
				surfaceTool.AddVertex(vertex);
			}
		}
		// ... add indices as before
	}
}
