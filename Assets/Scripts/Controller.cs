using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]

public class Controller : MonoBehaviour, ISpeed
{
    public float normalSpeed = 25f;
    public float accelerationSpeed = 45f;
    //public Transform cameraPosition;
    //public Camera mainCamera;
    public Transform spaceshipRoot;
    public float rotationSpeed = 2.0f;
    public float cameraSmooth = 4f;

    float speed;
    Rigidbody r;
    Quaternion lookRotation;
    float rotationZ = 0;
    float mouseXSmooth = 0;
    float mouseYSmooth = 0;
    Vector3 defaultShipRotation;

    public float SpeedRatio { get { return normalSpeed; } }


    // Start is called before the first frame update
    void Start()
    {
        r = GetComponent<Rigidbody>();
        r.useGravity = false;
        lookRotation = transform.rotation;
        defaultShipRotation = spaceshipRoot.localEulerAngles;
        rotationZ = defaultShipRotation.z;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    private float _t = 0.0f;
    //Rotation
    private float rotationZTmp = 0;

    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            _t += Time.deltaTime * 3.0f;
            _t = Mathf.Clamp(_t, 0, 1);
            speed = Mathf.Lerp(speed, accelerationSpeed, _t);
        }

        if (Input.GetMouseButtonUp(1))
        {
            speed = 0f;
            _t = 0;
        }

        if (Input.GetKey(KeyCode.A))
        {
            rotationZTmp = 1;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            rotationZTmp = -1;
        }
    }

    void FixedUpdate()
    {
        //Press Right Mouse Button to accelerate

        /*else
        {
            speed = Mathf.Lerp(speed, normalSpeed, Time.deltaTime * 10);
        }*/

        //Set moveDirection to the vertical axis (up and down keys) * speed
        Vector3 moveDirection = new Vector3(0, 0, speed);
        //Transform the vector3 to local space
        moveDirection = transform.TransformDirection(moveDirection);
        //Set the velocity, so you can move
        r.velocity = new Vector3(moveDirection.x, moveDirection.y, moveDirection.z);

        //Camera follow
        //mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, cameraPosition.position, Time.deltaTime * cameraSmooth);
        //mainCamera.transform.rotation = Quaternion.Lerp(mainCamera.transform.rotation, cameraPosition.rotation, Time.deltaTime * cameraSmooth);



        mouseXSmooth = Mathf.Lerp(mouseXSmooth, Input.GetAxis("Mouse X") * rotationSpeed, Time.deltaTime * cameraSmooth);
        mouseYSmooth = Mathf.Lerp(mouseYSmooth, Input.GetAxis("Mouse Y") * rotationSpeed, Time.deltaTime * cameraSmooth);
        Quaternion localRotation = Quaternion.Euler(-mouseYSmooth, mouseXSmooth, rotationZTmp * rotationSpeed);
        lookRotation = lookRotation * localRotation;
        transform.rotation = lookRotation;
        rotationZ -= mouseXSmooth;
        rotationZ = Mathf.Clamp(rotationZ, -45, 45);
        spaceshipRoot.transform.localEulerAngles = new Vector3(defaultShipRotation.x, defaultShipRotation.y, rotationZ);
        rotationZ = Mathf.Lerp(rotationZ, defaultShipRotation.z, Time.deltaTime * cameraSmooth);



    }
}