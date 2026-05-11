using UnityEngine;

public class MiasmaControler : MonoBehaviour
{
    private float rateOfMiasma = 1f;
    private bool incSize = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Double the size of the object
        transform.localScale = new Vector3(2f, 2f, 2f);
    }

    void Update()
    {
        // Gradually increase size over time
        if(incSize)
            transform.localScale += new Vector3(rateOfMiasma,rateOfMiasma,rateOfMiasma) * Time.deltaTime;
    }

    public void flipSize()
    {
        this.incSize = !incSize;
    }
}
