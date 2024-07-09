using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vertigo2 {
    /// <summary>
    /// a trigger zone where objects should not interact with water. Intended for the inside of boats below the waterline
    /// </summary>
    public class VWaterMask : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            VWaterBody b = other.attachedRigidbody?.GetComponent<VWaterBody>();
            if (b != null) b.EnterWaterMask();
        }

        private void OnTriggerExit(Collider other)
        {
            VWaterBody b = other.attachedRigidbody?.GetComponent<VWaterBody>();
            if (b != null) b.ExitWaterMask();
        }

    }
}