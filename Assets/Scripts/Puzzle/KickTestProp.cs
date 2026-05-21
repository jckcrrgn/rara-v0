using UnityEngine;

/// <summary>
/// Test scaffold prop. Lives on every Rigidbody object in the KickTestScaffold
/// scene. Two responsibilities:
///
///   1. Capture initial transform on Start so KickTestScaffold.Reset() can
///      restore the row of props to their starting positions after they've
///      been kicked into disarray.
///   2. Expose a public "displayMass" so the scaffold's on-screen overlay can
///      label each prop with its mass at a glance, without rummaging through
///      Rigidbody.mass on every prop every frame.
///
/// Drop this on each prop alongside the Rigidbody/Collider. Set the
/// displayLabel in the inspector (or leave blank to auto-generate from mass).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class KickTestProp : MonoBehaviour
{
	[Tooltip("Optional label for the on-screen overlay. If blank, defaults to " +
		"the Rigidbody's mass formatted as e.g. '5kg'. Set explicitly for props " +
		"that represent named L6 objects: 'lamp', 'nightstand', 'shard'.")]
	[SerializeField] private string displayLabel;

	private Rigidbody rb;
	private Vector3 initialPosition;
	private Quaternion initialRotation;

	public string DisplayLabel =>
		string.IsNullOrEmpty(displayLabel) ? $"{rb.mass:F0}kg" : displayLabel;

	public Rigidbody Rb => rb;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		initialPosition = transform.position;
		initialRotation = transform.rotation;
	}

	/// <summary>
	/// Called by KickTestScaffold on the R-reset path. Snap back to starting
	/// pose, zero velocities so a kicked-and-tumbling prop comes to a complete
	/// rest instead of continuing its momentum from the new position.
	/// </summary>
	public void ResetToStart()
	{
		rb.linearVelocity = Vector3.zero;
		rb.angularVelocity = Vector3.zero;
		transform.SetPositionAndRotation(initialPosition, initialRotation);
	}
}
