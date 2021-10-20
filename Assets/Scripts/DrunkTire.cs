using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrunkTire : MonoBehaviour, ISpeed
{
    [Header("Dimensions")]
    [SerializeField] SphereCollider m_SphCollider;
    public float Radius { get { return m_SphCollider.radius; } }

    [Header("Translation from Inputs")]
    [SerializeField] float m_MaxTranslationSpeed;   //unit: m/s
    float m_TranslationSpeed;
    [SerializeField] float m_Acceleration;          //unit: m/s2
    [SerializeField] float m_Deceleration;          //unit: m/s2

    // SpeedRatio = Current translation speed / Max translation Speed 
    // m_TranslationSpeed is clamped between 0 and m_MaxTranslationSpeed
    // so this ratio takes values between 0 and 1
    public float SpeedRatio { get { return Mathf.Abs(m_TranslationSpeed) / m_MaxTranslationSpeed; } }

    [Header("Rotation from Inputs")]
    [SerializeField] float m_RotationSpeed;             //unit: degree/s
    [SerializeField] float m_RotationLerpCoef = 4;

    [Header("Oscillations")]
    [SerializeField] float m_OscillationAngularAmplitude = 20f;     //unit: degree
    [SerializeField] float m_OscillationBeat;                       //unit: turns/s
    float m_PrevOscillationAngle;

    [Header("Gfx Transform")]
    [SerializeField] Transform m_TireGfxTransform;
    Quaternion m_TireGfxInitLocalOrientation;
    float m_TireGfxRotCumulativeAngle = 0;

    Transform m_Transform;
    Rigidbody m_Rigidbody;

    private void Awake()
    {
        m_Transform = GetComponent<Transform>();
        m_Rigidbody = GetComponent<Rigidbody>();
    }
    private void Start()
    {
        m_PrevOscillationAngle = 0;
        m_TireGfxInitLocalOrientation = m_TireGfxTransform.localRotation;
    }

    bool GetPositionAndNormalOnTerrain(Vector3 pos, ref Vector3 normalOnTerrain, ref Vector3 posOnTerrain)
    {
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up, Vector3.down, out hit, float.PositiveInfinity, 1 << LayerMask.NameToLayer("terrain")))
        {
            posOnTerrain = hit.point;
            normalOnTerrain = hit.normal;
            return true;
        }
        return false;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        float hInput = Input.GetAxis("Horizontal");
        float vInput = Mathf.Max(0, Input.GetAxis("Vertical"));

        Vector3 newPos = m_Transform.position;
        Vector3 newNormal = m_Transform.up;

        //TRANSLATION
        m_TranslationSpeed = Mathf.Clamp(m_TranslationSpeed + (vInput > 0 ? m_Acceleration : -m_Deceleration) * Time.fixedDeltaTime, 0, m_MaxTranslationSpeed);

        if (GetPositionAndNormalOnTerrain(m_Transform.position + m_Transform.forward * m_TranslationSpeed * Time.fixedDeltaTime,
                          ref newNormal, ref newPos))
        {
            m_Rigidbody.MovePosition(newPos);
            m_TireGfxRotCumulativeAngle += Mathf.Rad2Deg * Vector3.Distance(m_Transform.position, newPos) / Radius; //<TROU 1>
        }

        m_Rigidbody.velocity = Vector3.zero;

        m_TireGfxTransform.localRotation = Quaternion.AngleAxis(m_TireGfxRotCumulativeAngle, Vector3.right);  //<TROU 2>
        //m_TireGfxTransform.localRotation = Quaternion.AngleAxis(m_TireGfxRotCumulativeAngle, Vector3.right)*Quaternion.Slerp(Quaternion.identity,m_TireGfxInitLocalOrientation,SpeedRatio);

        //ROTATION
        Quaternion currOrientation = m_Transform.rotation;

        //OSCILLATIONS
        float oscillationAngle = SpeedRatio * m_OscillationAngularAmplitude * Mathf.Sin(Time.fixedTime * m_OscillationBeat * Mathf.PI * 2f);    //< TROU 3 >
        float oscillationDeltaAngle = oscillationAngle - m_PrevOscillationAngle;
        m_PrevOscillationAngle = oscillationAngle;

        Quaternion oscillationRot = Quaternion.AngleAxis(oscillationDeltaAngle, m_Transform.forward); //<TROU 4>

        //ROTATION TO ALIGN UP VECTOR TO THE TERRAIN NORMAL
        Quaternion alignUpWithTerrainNormalQuaternion = Quaternion.FromToRotation(m_Transform.up, newNormal); //< TROU 5 > // the quaternion that aims at aligning the UP vector of the tire with the terrain normal (newNormal)
        Quaternion newOrientation = Quaternion.Slerp(currOrientation, alignUpWithTerrainNormalQuaternion * currOrientation, Time.fixedDeltaTime * m_RotationLerpCoef);

        float inputRotDeltaAngle = m_RotationSpeed * hInput * Time.fixedDeltaTime;
        Quaternion inputRot = Quaternion.AngleAxis(inputRotDeltaAngle, m_Transform.up); //<TROU 6>
        newOrientation = oscillationRot * inputRot * newOrientation; //<TROU 7>
        m_Rigidbody.MoveRotation(newOrientation);
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, Screen.height - 40, 200, 40), "Use the arrows to steer the tire");
    }
}
