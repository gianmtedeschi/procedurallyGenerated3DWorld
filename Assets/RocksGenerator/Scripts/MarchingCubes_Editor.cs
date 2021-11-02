#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(MarchingCubes))]
public class MarchingCubes_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MarchingCubes generator = (MarchingCubes)target;

        if (GUILayout.Button("Generate"))
        {
            generator.Generate();
        }

        if (GUILayout.Button("SaveAssets"))
        {
            generator.SaveAssets();
        }

        if (GUILayout.Button("SDFVisualizer"))
        {
            if (((MarchingCubes)target)._sdf == null)
                return;

            Texture3DVisualizer.Show(((MarchingCubes)target)._fullSdf);
        }

        
    }

 

}


public class Texture3DVisualizer : EditorWindow
{
    private static Rect _rect = new Rect(100, 100, 500, 500);
    private static Rect _imageRect = new Rect(50, 50, 400, 400);

    private RenderTexture _tex;
    private Texture2D _displayTex;
    private float _level = 0;
    public static void Show(RenderTexture tex)
    {
        EditorWindow.GetWindowWithRect<Texture3DVisualizer>(_rect, false, "Texture3D", true)._tex=tex;

    }

    private void OnGUI()
    {
        _level=EditorGUILayout.Slider(_level, 0, 1);

        _displayTex = new Texture2D(_tex.width, _tex.height, _tex.graphicsFormat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);

        _displayTex.filterMode = FilterMode.Point;

        Graphics.CopyTexture(_tex, Mathf.Min((int)(_level * _tex.volumeDepth), _tex.volumeDepth-1), _displayTex, 0);

        GUI.DrawTexture(_imageRect, _displayTex, ScaleMode.ScaleToFit, false, 0);
    }


}


#endif