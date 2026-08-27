using UnityEditor;

public static class AssetBundleBuilder
{
	[MenuItem("Tools/Build AssetBundles")]
	static void Build()
	{
		BuildPipeline.BuildAssetBundles(
			"AssetBundles",
			BuildAssetBundleOptions.ChunkBasedCompression,
			BuildTarget.WebGL
		);
	}
}