using UnityEditor;
using UnityEngine;

namespace CascadeRoll.Editor {
  /// <summary>
  /// Adds a "Simulate" button under any RollTableAsset: rolls the asset's tree N times with no modifiers and shows the histogram.
  /// </summary>
  [CustomEditor(typeof(RollTableAsset<>), true)]
  public class RollTableAssetEditor : UnityEditor.Editor {
    private int _rolls = 10000;
    private string _report;

    public override void OnInspectorGUI() {
      DrawDefaultInspector();
      EditorGUILayout.Space();
      EditorGUILayout.BeginHorizontal();
      _rolls = Mathf.Max(1, EditorGUILayout.IntField("Rolls", _rolls));
      if (GUILayout.Button("Simulate", GUILayout.Width(90f))) {
        _report = Simulate();
      }
      EditorGUILayout.EndHorizontal();
      if (!string.IsNullOrEmpty(_report)) {
        EditorGUILayout.HelpBox(_report, MessageType.None);
      }
    }

    // The asset is RollTableAsset<T> for some T only known at run time, so ToTable and the roller are reached by reflection once.
    private string Simulate() {
      try {
        System.Type assetType = target.GetType();
        System.Type baseType = assetType;
        while (baseType != null && (!baseType.IsGenericType || baseType.GetGenericTypeDefinition() != typeof(RollTableAsset<>))) {
          baseType = baseType.BaseType;
        }
        if (baseType == null) {
          return "Not a RollTableAsset.";
        }
        System.Type itemType = baseType.GetGenericArguments()[0];
        object table = baseType.GetMethod("ToTable").Invoke(target, null);
        object roller = System.Activator.CreateInstance(typeof(Roller<,>).MakeGenericType(itemType, typeof(object)), new object[] { null, null });
        System.Reflection.MethodInfo run = typeof(RollSimulator).GetMethod("Run").MakeGenericMethod(itemType, typeof(object));
        object report = run.Invoke(null, new object[] { roller, table, null, _rolls, 0, null });
        return "Base odds, no modifiers.\n" + report;
      } catch (System.Exception e) {
        return (e.InnerException ?? e).Message;
      }
    }
  }
}
