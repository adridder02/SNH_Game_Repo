using UnityEngine;

public class playerAnimation : MonoBehaviour
{
    private bool isStill = false;
    private bool isInAir = false;
    private Animator playerAni;
    
    void Start()
    {
        playerAni = GetComponent<Animator>();
        
        if (playerAni == null)
        {
            Debug.LogError("Animator component not found on " + gameObject.name);
        }
    }
    
    
    public void setIdel()
    {
        if (playerAni != null && playerAni.GetFloat("Speed") != 0.5f)
            playerAni.SetFloat("Speed", 0.5f);
        isStill = true;
        //Debug.Log("We Still");
    }
    
    public void setWalking()
    {
        if (playerAni != null && playerAni.GetFloat("Speed") != 1.5f)
            playerAni.SetFloat("Speed", 1.5f);
        isStill = false;
    }
    
    public void setRunning()
    {
        if (playerAni != null && playerAni.GetFloat("Speed") != 3.5f)
            playerAni.SetFloat("Speed", 3.5f);
        isStill = false;
    }
    
    public void jump()
    {
        if (playerAni != null)
        {
            playerAni.SetTrigger("Jump");
        }
    }
    
    public void fly()
    {
        if (playerAni != null && !playerAni.GetBool("IsFlying"))
            playerAni.SetBool("IsFlying", true);
        isInAir = true;
        //Debug.Log("We flying");
    }
    
    public void notInAir()
    {
        if (playerAni != null)
        {
            playerAni.SetBool("Jump", false);
            playerAni.SetBool("IsFlying", false);
            isInAir = false;
        }
    }
    
    public void setJumpFalse()
    {
        if (playerAni != null)
            playerAni.SetBool("Jump", false);
    }

    private void tiltNeutral(){
        //Debug.Log("Air: "+ isInAir +" -> Still: "+ isStill);
        if(!isInAir || !isStill)
            return;
        Vector3 rotation = transform.eulerAngles;
        
        // Fix: Need to normalize angles to -180 to 180 range first
        float xAngle = rotation.x;
        if (xAngle > 180f) xAngle -= 360f; // Convert to -180..180 range
        
        if (xAngle < -2f)
            rotation.x += 1f;
        else if (xAngle > 2f)
            rotation.x -= 1f; // FIX: was "1=" which is syntax error

        transform.eulerAngles = rotation;
    }
    void Update(){
        tiltNeutral();
    }
}