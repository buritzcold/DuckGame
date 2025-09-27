using UnityEngine;
using System.Collections.Generic;

public class GroundCheck : MonoBehaviour
{
    [Tooltip("Set to Ground layer")]
    public LayerMask groundLayer;

    public int wallThreshold = 2;

    private HashSet<Collider> touchingGrounds = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & groundLayer) != 0)
        {
            touchingGrounds.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & groundLayer) != 0)
        {
            touchingGrounds.Remove(other);
        }
    }

    public bool ShouldTurnAround()
    {
        int count = touchingGrounds.Count;
        return count == 0 || count >= wallThreshold;
    }
}
