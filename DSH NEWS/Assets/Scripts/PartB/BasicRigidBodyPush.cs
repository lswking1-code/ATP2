using UnityEngine;

public class BasicRigidBodyPush : MonoBehaviour
{
	public LayerMask pushLayers;
	public bool canPush;
	[Range(0.5f, 5f)] public float strength = 1.1f;

	private void OnControllerColliderHit(ControllerColliderHit hit)
	{
		if (canPush) PushRigidBodies(hit);
	}

	private void PushRigidBodies(ControllerColliderHit hit)
	{

		// 确保碰到的刚体存在且不是运动学刚体
		Rigidbody body = hit.collider.attachedRigidbody;
		if (body == null || body.isKinematic) return;

		// 确保只推动指定的图层
		var bodyLayerMask = 1 << body.gameObject.layer;
		if ((bodyLayerMask & pushLayers.value) == 0) return;

		// 不要推动位于我们下方的物体
		if (hit.moveDirection.y < -0.3f) return;

		// 根据移动方向计算推动方向，仅保留水平分量
		Vector3 pushDir = new Vector3(hit.moveDirection.x, 0.0f, hit.moveDirection.z);

		// 应用推动力并考虑强度
		body.AddForce(pushDir * strength, ForceMode.Impulse);
	}
}