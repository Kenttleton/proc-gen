using Godot;

public partial class PropMeshGenerator : Node
{
	public static Mesh CreateTreeMesh()
	{
		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		// Trunk (cylinder)
		var trunk = new CylinderMesh();
		trunk.TopRadius = 0.2f;
		trunk.BottomRadius = 0.3f;
		trunk.Height = 3.0f;

		// Foliage (sphere on top)
		var foliage = new SphereMesh();
		foliage.Radius = 1.5f;
		foliage.Height = 3.0f;

		// For now, just return trunk
		// TODO: Combine meshes properly
		return trunk;
	}

	public static Mesh CreateRockMesh()
	{
		var mesh = new SphereMesh();
		mesh.Radius = 0.8f;
		mesh.Height = 1.0f;
		mesh.RadialSegments = 8; // Low-poly look
		mesh.Rings = 4;
		return mesh;
	}

	public static Mesh CreateBushMesh()
	{
		var mesh = new SphereMesh();
		mesh.Radius = 0.6f;
		mesh.Height = 1.2f;
		return mesh;
	}

	public static Mesh CreateGrassPatch()
	{
		// Simple cross-quad for grass
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);

		Vector3[] vertices = {
			new Vector3(-0.3f, 0, 0), new Vector3(0.3f, 0.5f, 0),
			new Vector3(0.3f, 0, 0), new Vector3(-0.3f, 0.5f, 0),
			new Vector3(0, 0, -0.3f), new Vector3(0, 0.5f, 0.3f),
			new Vector3(0, 0, 0.3f), new Vector3(0, 0.5f, -0.3f),
		};

		int[] indices = { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };

		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.Index] = indices;

		var arrayMesh = new ArrayMesh();
		arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		return arrayMesh;
	}
}