using Godot;

public partial class StarTextureGenerator : Node
{
	public static NoiseTexture2D GenerateStarTexture()
	{
		// Create procedural star field
		var noise = new FastNoiseLite();
		noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
		noise.Frequency = 0.05f;
		noise.CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean;
		noise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance;

		var noiseTexture = new NoiseTexture2D();
		noiseTexture.Noise = noise;
		noiseTexture.Width = 1024;
		noiseTexture.Height = 1024;
		noiseTexture.Seamless = true;

		return noiseTexture;
	}
}