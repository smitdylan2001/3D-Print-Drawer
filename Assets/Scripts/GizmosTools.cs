using Oculus.Interaction;
using UnityEngine;

public class GizmosTools : MonoBehaviour
{
    public Grabbable grab;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void InjectOptionalOneGrabTransformer(GrabFreeTransformer transformer)
    {
        grab.InjectOptionalOneGrabTransformer(transformer);
    }
}
