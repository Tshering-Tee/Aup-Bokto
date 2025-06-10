using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    public Rigidbody playerRigidbody;
    public float forwardSpeed;
    public float jumpPower;
    public bool hadJumped = false;
    public bool jumpRequest;
    Vector3 mousePosDown;
    Vector3 mousePosUp;
    public Transform player;
    // Start is called before the first frame update
    void Start()
    {
        playerRigidbody = GetComponent<Rigidbody>();
        player = this.transform;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            hadJumped = true;
        }
    }

    // figure out the swapping
    public void FigureSwap()
    {
        float deltaX = mousePosUp.x - mousePosDown.x;
        float deltaY = mousePosUp.y - mousePosDown.y;

        if (Mathf.Abs(deltaX) > Mathf.Abs(deltaY))
        {
            if (deltaX > 0)
            {
                Debug.Log("Swap right");
                player.eulerAngles = new Vector3(playerRigidbody.velocity.x, 90, playerRigidbody.velocity.z);
                Vector3 movement = new Vector3(playerRigidbody.velocity.x, playerRigidbody.velocity.y, forwardSpeed);

            }
            else if (deltaX < 0)
            {
                Debug.Log("Swap Left");
                player.eulerAngles = new Vector3(playerRigidbody.velocity.x, -90, playerRigidbody.velocity.z);
            }
        }
        else
        {
            if (deltaY > 0)
            {
                Debug.Log("Swap up");
            }
            else if (deltaY < 0)
            {
                Debug.Log("Swap down");
            }
        }
    }

    void Update()
    {

        if (Input.GetKeyDown("space") && hadJumped)
        {
            jumpRequest = true;
        }

        // horizontal and vertical movement of player
        if (Input.GetMouseButtonDown(0))
        {
            mousePosDown = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            mousePosUp = Input.mousePosition;
            FigureSwap();
            // if (mousePosDown.x - mousePosUp.x < 0)
            // {
            //     Debug.Log("Rigt Swap");
            // }
            // else
            // {
            //     Debug.Log("Left Swap");
            // }

            // if (mousePosDown.y - mousePosUp.y < 0)
            // {
            //     Debug.Log("Swap up");
            // }
            // else
            // {
            //     Debug.Log("Swap Down");
            // }
        }

    }
    void FixedUpdate()
    {
        //playerMovement
        // Move the player in the direction they are facing
        Vector3 movementDirection = transform.forward * forwardSpeed;
        Vector3 movement = new Vector3(movementDirection.x, playerRigidbody.velocity.y, movementDirection.z);
        playerRigidbody.velocity = movement;

        //jump
        if (jumpRequest)
        {
            var jump = new Vector3(playerRigidbody.velocity.x, jumpPower, playerRigidbody.velocity.z);
            playerRigidbody.AddForce(jump, ForceMode.Impulse);
            hadJumped = false;
            jumpRequest = false;
        }
    }
}
