using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{

    [SerializeField] Transform m_LookAtTarget;

    [SerializeField] Component m_Speed;

    [SerializeField] float m_MinAngle;
    [SerializeField] float m_MaxAngle;
    [SerializeField] float m_OffsetAngle;

    float Angle { get { return Mathf.Lerp(m_MinAngle, m_MaxAngle, 1f - (m_Speed as ISpeed).SpeedRatio); } }
    float Distance { get { return Mathf.Lerp(m_MinDistance, m_MinDistance*1.25f, (m_Speed as ISpeed).SpeedRatio); } }

    Transform m_Transform;
    float m_MinDistance;

    Vector3 positionUnitVect;

    [SerializeField] float m_OrientationLerpCoef = 3;
    [SerializeField] float m_PositionLerpCoef = 34;

    private void Awake()
    {
        m_Transform = GetComponent<Transform>();
    }

    // Use this for initialization
    void Start()
    {
        m_MinDistance = Vector3.Distance(m_Transform.position, m_LookAtTarget.position);
        positionUnitVect = Vector3.ProjectOnPlane(m_LookAtTarget.forward, Vector3.up).normalized;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        positionUnitVect = Vector3.Slerp(positionUnitVect, Vector3.ProjectOnPlane(m_LookAtTarget.forward, Vector3.up).normalized,Time.deltaTime* m_OrientationLerpCoef);

        m_Transform.position = Vector3.Slerp(   m_Transform.position,
                                                m_LookAtTarget.position - Quaternion.AngleAxis(Angle, Vector3.ProjectOnPlane(m_LookAtTarget.right,Vector3.up).normalized) * positionUnitVect * Distance,
                                                Time.time* m_PositionLerpCoef);

        m_Transform.rotation = Quaternion.AngleAxis(m_OffsetAngle,m_Transform.right)*Quaternion.LookRotation((m_LookAtTarget.position-m_Transform.position).normalized);
    }
}
