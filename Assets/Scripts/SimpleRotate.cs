using UnityEngine;

public class SimpleRotate : MonoBehaviour
{
    public Vector3 BasePoint = Vector3.zero;
    public float Speed = 0f;

    void Update()
    {
        transform.RotateAround(
            BasePoint, Vector3.up, Time.deltaTime * 360 * Speed);
    }
}
