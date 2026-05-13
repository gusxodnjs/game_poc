// HelloWorld.cs
// TERRA × BIOSPHERE PoC — Step 1 client smoke test.
//
// Renders "Hello, TERRA!" centered on screen using IMGUI so that no UI prefab,
// canvas, or font asset setup is required. Works in the Unity Editor, on iOS,
// and on Android with no additional configuration.
//
// Usage:
//   1. Open the project in Unity 6.0 LTS (6000.0.75f1).
//   2. In any active scene, create an empty GameObject (e.g. "HelloRoot").
//   3. Drag this script onto the GameObject, or click Add Component → HelloWorld.
//   4. Press Play (or build to device). The label is drawn every frame.
//
// Notes:
//   - Font size scales with screen height so it remains readable on phones.
//   - Color is white with a subtle drop shadow for readability over any
//     background color (including the default Unity skybox).

using UnityEngine;

public class HelloWorld : MonoBehaviour
{
    [Tooltip("Text to display. Defaults to the PoC greeting.")]
    public string message = "Hello, TERRA!";

    [Tooltip("Override font size. 0 = auto (scales with screen height).")]
    public int fontSizeOverride = 0;

    private GUIStyle _labelStyle;
    private GUIStyle _shadowStyle;

    private void EnsureStyles()
    {
        // GUIStyle must be created inside OnGUI; cache between frames.
        if (_labelStyle != null)
        {
            return;
        }

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };
        _labelStyle.normal.textColor = Color.white;

        _shadowStyle = new GUIStyle(_labelStyle);
        _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.55f);
    }

    private void OnGUI()
    {
        EnsureStyles();

        int size = fontSizeOverride > 0
            ? fontSizeOverride
            : Mathf.Max(40, Screen.height / 12);
        _labelStyle.fontSize = size;
        _shadowStyle.fontSize = size;

        Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
        Rect shadow = new Rect(full.x + 2f, full.y + 2f, full.width, full.height);

        GUI.Label(shadow, message, _shadowStyle);
        GUI.Label(full, message, _labelStyle);
    }
}
