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

    /// <summary>Falling — either walked off a ledge without jumping, or auto-descending after a
    /// double-space-while-flying. Now plays the real dragon_fall clip via its own "IsFalling" bool,
    /// rather than borrowing Fly Idle ("IsFlying") the way it did as a placeholder. Explicitly drops
    /// IsFlying too so the two states can't both be true at once and fight in the Animator (this
    /// matters for the auto-descend caller, which enters fall() while IsFlying is already true from
    /// EnterFlyMode()). Kept as its own method rather than PlayerController just calling fly()
    /// directly so any future changes to what "falling" plays stay a one-line change here.</summary>
    public void fall()
    {
        if (playerAni != null)
        {
            if (playerAni.GetBool("IsFlying"))
                playerAni.SetBool("IsFlying", false);
            if (!playerAni.GetBool("IsFalling"))
                playerAni.SetBool("IsFalling", true);
        }
        isInAir = true;
    }

    public void notInAir()
    {
        if (playerAni != null)
        {
            // "Jump" is set via SetTrigger() in jump() above, so it must be a Trigger parameter
            // in the Animator Controller, not a Bool - SetBool("Jump", false) here was a type
            // mismatch that Unity silently logs a warning for and ignores. ResetTrigger clears a
            // pending/consumed trigger the correct way for this parameter type. If "Jump" is
            // still set to Bool in the Controller's Parameters tab, change it to Trigger to match.
            playerAni.ResetTrigger("Jump");
            playerAni.SetBool("IsFlying", false);
            playerAni.SetBool("IsFalling", false); // clear the real fall animation too, not just Fly Idle
            isInAir = false;
        }
    }
    
    public void setJumpFalse()
    {
        if (playerAni != null)
            playerAni.ResetTrigger("Jump"); // see notInAir() above - "Jump" must be a Trigger, not a Bool
    }

    private void tiltNeutral(){
        //Debug.Log("Air: "+ isInAir +" -> Still: "+ isStill);
        // Only level out while grounded and idle (e.g. correcting drift after landing on a slope).
        // Previously ran whenever isInAir && isStill, which included the very first frame of
        // takeoff - isStill often hadn't been updated to false yet (only the Grounded branch in
        // PlayerController.UpdateAnimator() ever calls setWalking()/setRunning() to clear it), so
        // this was nudging transform.eulerAngles.x by hand at the same time PlayerController's own
        // Quaternion.Slerp flight rotation was driving the same transform - two different rotation
        // systems fighting over the same frame, which reads as a messy/stuttery takeoff.
        if(isInAir || !isStill)
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