using UnityEngine;

public class CameraTargetFollow : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] Vector3 worldOffset = Vector3.up * 1.5f;
    [SerializeField] Vector3 fixedEuler = new Vector3(60f, 0f, 0f);

    void LateUpdate()
    {
        if (!player) return;

        transform.position = player.position + worldOffset;
        transform.rotation = Quaternion.Euler(fixedEuler);
    }
}
