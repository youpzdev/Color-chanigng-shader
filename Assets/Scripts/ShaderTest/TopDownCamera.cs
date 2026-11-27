using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(8, 14, -12);

    private void Start()
    {
        transform.position = target.position + offset;
        transform.rotation = Quaternion.LookRotation(-offset);
    }

    private void LateUpdate()
    {
        transform.position = Vector3.Lerp(transform.position, target.position + offset, 1 - Mathf.Exp(-8 * Time.deltaTime));
        transform.rotation = Quaternion.LookRotation(-offset);
    }
}
