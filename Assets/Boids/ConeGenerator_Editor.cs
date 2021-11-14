#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(ConeGenerator))]
public class ConeGenerator_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ConeGenerator generator = (ConeGenerator)target;

        if (GUILayout.Button("Generate"))
        {
            generator.Create();
        }

    }



}


#endif