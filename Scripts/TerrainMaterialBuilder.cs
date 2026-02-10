using Godot;

public partial class TerrainMaterialBuilder : Node
{
	private ShaderMaterial _terrainMaterial;

	public ShaderMaterial CreateTerrainMaterial()
	{
		var shader = GD.Load<Shader>("res://Shaders/terrain.gdshader");
		_terrainMaterial = new ShaderMaterial();
		_terrainMaterial.Shader = shader;

		// Load textures (you'll need to add these to your project)
		// You can find free PBR textures at polyhaven.com or ambientcg.com
		var grassTexture = CreateGrassTexture(); // Or load: GD.Load<Texture2D>("res://textures/grass.png")
		var rockTexture = CreateRockTexture();
		var snowTexture = CreateSnowTexture();
		var dirtTexture = CreateDirtTexture();

		_terrainMaterial.SetShaderParameter("grass_texture", grassTexture);
		_terrainMaterial.SetShaderParameter("rock_texture", rockTexture);
		_terrainMaterial.SetShaderParameter("snow_texture", snowTexture);
		_terrainMaterial.SetShaderParameter("dirt_texture", dirtTexture);

		// Set height thresholds based on your terrain
		_terrainMaterial.SetShaderParameter("grass_height_min", -5.0f);
		_terrainMaterial.SetShaderParameter("grass_height_max", 3.0f);
		_terrainMaterial.SetShaderParameter("rock_height_min", 2.0f);
		_terrainMaterial.SetShaderParameter("rock_height_max", 8.0f);
		_terrainMaterial.SetShaderParameter("snow_height_min", 7.0f);
		_terrainMaterial.SetShaderParameter("texture_scale", 0.05f);

		return _terrainMaterial;
	}

	// Procedural textures (if you don't have image files)
	private Texture2D CreateGrassTexture()
	{
		var image = Image.CreateEmpty(256, 256, false, Image.Format.Rgb8);

		for (int y = 0; y < 256; y++)
		{
			for (int x = 0; x < 256; x++)
			{
				// Vary green shades
				float noise = GD.Randf();
				float green = 0.3f + noise * 0.3f;
				image.SetPixel(x, y, new Color(green * 0.3f, green, green * 0.2f));
			}
		}

		return ImageTexture.CreateFromImage(image);
	}

	private Texture2D CreateRockTexture()
	{
		var image = Image.CreateEmpty(256, 256, false, Image.Format.Rgb8);

		for (int y = 0; y < 256; y++)
		{
			for (int x = 0; x < 256; x++)
			{
				float noise = GD.Randf();
				float gray = 0.3f + noise * 0.3f;
				image.SetPixel(x, y, new Color(gray, gray, gray));
			}
		}

		return ImageTexture.CreateFromImage(image);
	}

	private Texture2D CreateSnowTexture()
	{
		var image = Image.CreateEmpty(256, 256, false, Image.Format.Rgb8);

		for (int y = 0; y < 256; y++)
		{
			for (int x = 0; x < 256; x++)
			{
				float noise = GD.Randf();
				float white = 0.85f + noise * 0.15f;
				image.SetPixel(x, y, new Color(white, white, white));
			}
		}

		return ImageTexture.CreateFromImage(image);
	}

	private Texture2D CreateDirtTexture()
	{
		var image = Image.CreateEmpty(256, 256, false, Image.Format.Rgb8);

		for (int y = 0; y < 256; y++)
		{
			for (int x = 0; x < 256; x++)
			{
				float noise = GD.Randf();
				float brown = 0.25f + noise * 0.15f;
				image.SetPixel(x, y, new Color(brown * 1.2f, brown * 0.8f, brown * 0.5f));
			}
		}

		return ImageTexture.CreateFromImage(image);
	}
}