using UnityEngine;

/// <summary>
/// Attached to dynamically spawned mock characters during stress testing.
/// Simulates randomized movement speeds and footstep intervals to stress-test 
/// the voice virtualization and rendering capacity of the footstep audio system.
/// </summary>
public class MockCharacterWalker : MonoBehaviour
{
    private DualFootstepController controller;
    private float nextStepTime;
    private float minInterval = 0.2f;
    private float maxInterval = 0.8f;

    private void Start()
    {
        controller = GetComponent<DualFootstepController>();
        ScheduleNextStep();
    }

    private void Update()
    {
        if (Time.time >= nextStepTime)
        {
            TriggerMockStep();
            ScheduleNextStep();
        }
    }

    private void ScheduleNextStep()
    {
        nextStepTime = Time.time + Random.Range(minInterval, maxInterval);
    }

    private void TriggerMockStep()
    {
        if (controller == null || controller.feet == null || controller.feet.Count == 0) return;

        // Choose a random foot
        int footIdx = Random.Range(0, controller.feet.Count);
        var foot = controller.feet[footIdx];

        // Simulate a randomized movement speed (velocity)
        float simulatedSpeed = Random.Range(0.5f, controller.maxSpeed);

        if (foot.isLeft)
        {
            controller.StepLeft(simulatedSpeed);
        }
        else
        {
            controller.StepRight(simulatedSpeed);
        }
    }
}
