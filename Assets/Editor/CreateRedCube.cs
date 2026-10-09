using UnityEngine;
using UnityEditor;

public class CreateRedCube
{
    [MenuItem("Tools/赤いCubeを作成")]
    public static void Create()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "RedCube";

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.red;

        cube.GetComponent<Renderer>().material = mat;

        Selection.activeGameObject = cube;
        Undo.RegisterCreatedObjectUndo(cube, "Create Red Cube");

        Debug.Log("赤いCubeを作成しました: " + cube.name);
    }
}
