using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] float m_TranslationSpeed;
    [SerializeField] float m_RotationSpeed;

    Rigidbody m_Rigidbody;

	private void Awake()
	{
        m_Rigidbody = GetComponent<Rigidbody>();
    }

	// Start is called before the first frame update
	void Start()
    {
        
    }

    // kinematic mvt -> position and orientation are forced by script
    // transform,  Update(), deltaTime
    // once a line of code has been executed, position and orientation have actually changed

    // Update is called once per frame
    void Update()
    {
        //float vInput = Input.GetAxis("Vertical"); // a value between -1 and 1 -> trabnslation
        //float hInput = Input.GetAxis("Horizontal"); // rotation

        ////kinematic
        //transform.position += transform.forward * vInput * Time.deltaTime * m_TranslationSpeed;
        //transform.rotation = Quaternion.AngleAxis(hInput*Time.deltaTime* m_RotationSpeed,Vector3.up) * transform.rotation;
    }

	//dynamic mvt -> pos & orient are computed by the PhysX engine 
	// rigidbody , FixedUpdate(), fixedDeltaTime
	// a line of code is just a request to the PhysX engine .... all (all GameObjects & components) requests are treated and computed at the end of the frame

	private void FixedUpdate()
	{
        float vInput = Input.GetAxis("Vertical"); // a value between -1 and 1 -> trabnslation
        float hInput = Input.GetAxis("Horizontal"); // rotation

        // POSITION stuff: a pseudo kinematic behaviour .... position & rotation will be "forced" by script, but collision will happen with obstacles 
        //Vector3 moveVect = transform.forward * vInput * Time.fixedDeltaTime * m_TranslationSpeed;
        //m_Rigidbody.MovePosition(transform.position + moveVect);
        //m_Rigidbody.MoveRotation(Quaternion.AngleAxis(hInput * Time.fixedDeltaTime * m_RotationSpeed, Vector3.up) * transform.rotation);

        //m_Rigidbody.angularVelocity = Vector3.zero;
        //m_Rigidbody.velocity = Vector3.zero;

        // VELOCITY stuff: use of AddForce method to change the velocity
        if (vInput != 0 || hInput != 0)
        {
            Vector3 velocityDelta = transform.forward * m_TranslationSpeed * vInput - m_Rigidbody.velocity;
            m_Rigidbody.AddForce(velocityDelta, ForceMode.VelocityChange);
        
            Vector3 angularVelocityDelta = Vector3.up * m_RotationSpeed*Mathf.Deg2Rad * hInput - m_Rigidbody.angularVelocity;
            m_Rigidbody.AddTorque(angularVelocityDelta, ForceMode.VelocityChange);
        }

        // FORCE mode -> all frictions with external objects will be taken into account
        //m_Rigidbody.AddForce(transform.forward * 1000 * vInput, ForceMode.Force);
        //m_Rigidbody.AddTorque(Vector3.up * 100 * hInput, ForceMode.Force);

    }

	private void OnCollisionEnter(Collision collision)
	{
        Debug.Log("colliding with " + collision.gameObject.name);
	}
}
