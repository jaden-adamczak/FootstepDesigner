using UnityEngine;

[RequireComponent(typeof(DualFootstepController))]
public class FootstepTestTrigger : MonoBehaviour
{
    private DualFootstepController controller;

    [Header("Test Key Bindings")]
    [Tooltip("Press this key in Play mode to trigger a left footstep.")]
    public KeyCode testLeftKey = KeyCode.Q;
    [Tooltip("Press this key in Play mode to trigger a right footstep.")]
    public KeyCode testRightKey = KeyCode.E;

    // Reflection caching for the New Input System
    private bool checkedReflection;
    private System.Type keyboardType;
    private System.Reflection.PropertyInfo currentKeyboardProperty;
    private System.Type keyEnumType;
    private System.Reflection.PropertyInfo indexerProperty;
    private System.Reflection.PropertyInfo wasPressedProperty;

    private void Start()
    {
        controller = GetComponent<DualFootstepController>();
        InitializeReflection();
    }

    private void InitializeReflection()
    {
        if (checkedReflection) return;
        checkedReflection = true;

        try
        {
            keyboardType = System.Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            if (keyboardType == null) return;

            currentKeyboardProperty = keyboardType.GetProperty("current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            keyEnumType = System.Type.GetType("UnityEngine.InputSystem.Key, Unity.InputSystem");
            if (keyEnumType == null) return;

            indexerProperty = keyboardType.GetProperty("Item", new System.Type[] { keyEnumType });
        }
        catch
        {
            // Ignore reflection errors on startup if the Input System package is not present
        }
    }

    private void Update()
    {
        bool leftPressed = false;
        bool rightPressed = false;

        // Try using the New Input System via Reflection first
        if (keyboardType != null && currentKeyboardProperty != null && indexerProperty != null)
        {
            leftPressed = GetNewInputKeyDownReflection(testLeftKey);
            rightPressed = GetNewInputKeyDownReflection(testRightKey);
        }
        else
        {
            // Fall back to legacy Input System
            // Wrapping in try-catch to prevent errors if the legacy input manager is completely disabled in Player Settings
            try
            {
                leftPressed = Input.GetKeyDown(testLeftKey);
                rightPressed = Input.GetKeyDown(testRightKey);
            }
            catch
            {
                // Legacy input is completely disabled in Player Settings
            }
        }

        if (leftPressed)
        {
            controller.StepLeft();
            Debug.Log("Triggered Left Footstep");
        }

        if (rightPressed)
        {
            controller.StepRight();
            Debug.Log("Triggered Right Footstep");
        }
    }

    private bool GetNewInputKeyDownReflection(KeyCode keyCode)
    {
        try
        {
            var keyboardInstance = currentKeyboardProperty.GetValue(null);
            if (keyboardInstance == null) return false;

            string keyName = keyCode.ToString();
            if (keyName.StartsWith("Alpha"))
            {
                keyName = "Digit" + keyName.Substring(5);
            }
            else if (keyName == "Return")
            {
                keyName = "Enter";
            }

            if (!System.Enum.TryParse(keyEnumType, keyName, true, out object keyValue))
            {
                return false;
            }

            var keyControlInstance = indexerProperty.GetValue(keyboardInstance, new object[] { keyValue });
            if (keyControlInstance == null) return false;

            if (wasPressedProperty == null)
            {
                wasPressedProperty = keyControlInstance.GetType().GetProperty("wasPressedThisFrame");
            }

            if (wasPressedProperty == null) return false;

            return (bool)wasPressedProperty.GetValue(keyControlInstance);
        }
        catch
        {
            return false;
        }
    }
}
