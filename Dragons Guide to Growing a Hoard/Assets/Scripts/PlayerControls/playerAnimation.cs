using UnityEngine;

public class playerAnimation : MonoBehaviour
{
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
    }
    
    public void setWalking()
    {
        if (playerAni != null && playerAni.GetFloat("Speed") != 1.5f)
            playerAni.SetFloat("Speed", 1.5f);
    }
    
    public void setRunning()
    {
        if (playerAni != null && playerAni.GetFloat("Speed") != 3.5f)
            playerAni.SetFloat("Speed", 3.5f);
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
    }
    
    public void notInAir()
    {
        if (playerAni != null)
        {
            playerAni.SetBool("Jump", false);
            playerAni.SetBool("IsFlying", false);
        }
    }
    
    public void setJumpFalse()
    {
        if (playerAni != null)
            playerAni.SetBool("Jump", false);
    }
}