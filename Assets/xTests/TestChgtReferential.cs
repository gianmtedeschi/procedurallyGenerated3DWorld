using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestChgtReferential : MonoBehaviour
{
    float cumulativeAngle = 0;
    [SerializeField] float rotSpeed;
    [SerializeField] Transform m_Child;

    Quaternion initLocatRot;
    private void Awake()
    {
        //float value = (.1f * Mathf.Cos(25 * Mathf.Deg2Rad) + .25f * Mathf.Cos(10 * Mathf.Deg2Rad)) * 50 * Mathf.Cos(20 * Mathf.Deg2Rad)
        //    + (.1f * Mathf.Sin(25 * Mathf.Deg2Rad) + .25f * Mathf.Sin(10 * Mathf.Deg2Rad)) * 50 * Mathf.Sin(20 * Mathf.Deg2Rad);
        //Debug.Log(value);
    }
    // Start is called before the first frame update
    void Start()
    {
        initLocatRot = m_Child.localRotation;
    }

    // Update is called once per frame
    void Update()
    {
        cumulativeAngle += Time.deltaTime * rotSpeed;
        m_Child.localRotation = initLocatRot* Quaternion.AngleAxis(cumulativeAngle, Vector3.right);
    }
}
