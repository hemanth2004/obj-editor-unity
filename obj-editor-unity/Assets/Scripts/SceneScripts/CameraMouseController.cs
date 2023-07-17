using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMouseController : MonoBehaviour {
	public float panSpeed, rotateSpeed;
	public float acceleration = 50; // how fast you accelerate
	public float accSprintMultiplier = 4; // how much faster you go when "sprinting"
	public float lookSensitivity = 1; // mouse look sensitivity
	public float dampingCoefficient = 5; // how quickly you break to a halt after you stop your input
	public bool focusOnEnable = true; // whether or not to focus and lock cursor immediately on enable

	Vector3 velocity; // current velocity

    public bool Focused;

	void OnEnable() {
		if( focusOnEnable ) Focused = true;
	}

	void OnDisable() => Focused = false;

	Vector3 panMovement = Vector3.zero;
	Vector3 prevMousePosition = Vector3.zero;

	void Update() {
		
        Vector3 mouseIn = new Vector3(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"), 0f);
        if (!Focused && Input.GetKey(KeyCode.Mouse2))
		{
            panMovement = mouseIn * panSpeed * Time.deltaTime;
			transform.position -= transform.up * panMovement.y;
            transform.position -= transform.right * panMovement.x;
        }
        if (!Focused && Input.GetKey(KeyCode.Mouse0))
        {
			Vector3 mouseDelta = Input.mousePosition - prevMousePosition;
            transform.RotateAround(Vector3.zero, Vector3.up, mouseDelta.x * rotateSpeed * Time.deltaTime);
            transform.RotateAround(Vector3.zero, transform.right, -mouseDelta.y * rotateSpeed * Time.deltaTime);
        }
		prevMousePosition = Input.mousePosition;




        // Input
        Focused = Input.GetKey(KeyCode.Mouse1);
        Cursor.visible = !Focused;
        
		if( Focused )
			UpdateInput();

		// Physics
		velocity = Vector3.Lerp( velocity, Vector3.zero, dampingCoefficient * Time.deltaTime );
		transform.position += (velocity  + transform.forward * acceleration * 10f * Input.GetAxis("Mouse ScrollWheel"))* Time.deltaTime ;
	}

	void UpdateInput() {
		// Position
		velocity += GetAccelerationVector() * Time.deltaTime;

		// Rotation
		Vector2 mouseDelta = lookSensitivity * new Vector2( Input.GetAxis( "Mouse X" ), -Input.GetAxis( "Mouse Y" ) );
		Quaternion rotation = transform.rotation;
		Quaternion horiz = Quaternion.AngleAxis( mouseDelta.x, Vector3.up );
		Quaternion vert = Quaternion.AngleAxis( mouseDelta.y, Vector3.right );
		transform.rotation = horiz * rotation * vert;

		
	}

	Vector3 GetAccelerationVector() {
		Vector3 moveInput = default;

		void AddMovement( KeyCode key, Vector3 dir ) {
			if( Input.GetKey( key ) )
				moveInput += dir;
		}

		AddMovement( KeyCode.W, Vector3.forward );
		AddMovement( KeyCode.S, Vector3.back );
		AddMovement( KeyCode.D, Vector3.right );
		AddMovement( KeyCode.A, Vector3.left );
		AddMovement( KeyCode.Space, Vector3.up );
		AddMovement( KeyCode.LeftControl, Vector3.down );
        AddMovement( KeyCode.E, Vector3.up );
        AddMovement( KeyCode.Q, -Vector3.up );

		Vector3 direction = transform.TransformVector( moveInput.normalized );

		if( Input.GetKey( KeyCode.LeftShift ) )
			return direction * ( acceleration * accSprintMultiplier ) + panMovement; // "sprinting"
		return direction * acceleration + panMovement; // "walking"
	}
}