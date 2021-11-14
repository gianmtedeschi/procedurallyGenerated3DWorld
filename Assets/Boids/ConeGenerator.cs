#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

public class ConeGenerator : MonoBehaviour
{

	public int subdivisions = 10;
	public float radius = 1f;
	public float height = 2f;

	 public void Create()
	{
		Mesh mesh = new Mesh();

		Vector3[] vertices = new Vector3[(subdivisions + 1) * 2 + 1];
		int[] triangles = new int[(subdivisions * 2) * 3];

		vertices[0] = Vector3.zero;
		for (int i = 0, n = subdivisions - 1; i < subdivisions; i++)
		{
			float ratio = (float)i / n;
			float r = ratio * (Mathf.PI * 2f);
			float x = Mathf.Cos(r) * radius;
			float z = Mathf.Sin(r) * radius;
			vertices[i + 1] = new Vector3(x, 0f, z);

			Debug.Log(ratio);
		}
		vertices[subdivisions + 1] = new Vector3(0f, height, 0f);

		// duplicate base for normals

		for (int i = 0, n = subdivisions - 1; i < subdivisions; i++)
		{
			float ratio = (float)i / n;
			float r = ratio * (Mathf.PI * 2f);
			float x = Mathf.Cos(r) * radius;
			float z = Mathf.Sin(r) * radius;
			vertices[i + 1 + (subdivisions + 1)] = new Vector3(x, 0f, z);
		}

		// construct bottom

		for (int i = 0, n = subdivisions - 1; i < n; i++)
		{
			int offset = i * 3;
			triangles[offset] = 0 + (subdivisions + 2);
			triangles[offset + 1] = i + 1 + (subdivisions + 2);
			triangles[offset + 2] = i + 2 + (subdivisions + 2);
		}

		// construct sides

		int bottomOffset = subdivisions * 3;
		for (int i = 0, n = subdivisions - 1; i < n; i++)
		{
			int offset = i * 3 + bottomOffset;
			triangles[offset] = i + 1;
			triangles[offset + 1] = subdivisions + 1;
			triangles[offset + 2] = i + 2;
		}

		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.RecalculateBounds();
		mesh.RecalculateNormals();

		SaveAssets(mesh);
	}

    string folderName = "Procedural_Boids";

    private void SaveAssets(Mesh coneMesh)
    {
        string parentFolder = @"Assets";
        string assetsFolder = $@"{parentFolder}\{folderName}";
        string guid;

        if (!AssetDatabase.IsValidFolder(assetsFolder))
            guid = AssetDatabase.CreateFolder(parentFolder, folderName);
        else
            guid = AssetDatabase.AssetPathToGUID(assetsFolder);

        #region Meshes
        if (coneMesh != null)
        {


            string path = $@"{parentFolder}\{folderName}\coneMesh.asset";

            if (AssetDatabase.LoadAssetAtPath(path, typeof(Mesh)) != null)
            {
                // probably it's not the way it's meant to be done....but time's up
                AssetDatabase.DeleteAsset(path);

            }

            AssetDatabase.CreateAsset(coneMesh, path);


        }
        #endregion

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }


}
#endif