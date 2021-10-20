using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UnityEngine.CharacterController))]

public class MyCharacterController : MonoBehaviour, ISpeed
{

    [SerializeField] CharacterController charController;
    [SerializeField] float maxTranslationSpeed = 10; // m/s
    [SerializeField] float rotationSpeed;
    float translationSpeed;

    public float SpeedRatio
    {
        get
        {
            return Mathf.Abs(translationSpeed / maxTranslationSpeed);
        }
    }

    private void Awake()
    {
        if (!charController)
            charController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        float hInput = Input.GetAxis("Horizontal");
        float vInput = Input.GetAxis("Vertical");

        translationSpeed = Mathf.Lerp(-maxTranslationSpeed, maxTranslationSpeed, (vInput + 1) * .5f);

        charController.Move(transform.forward * translationSpeed);
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime * hInput);
    }
}
