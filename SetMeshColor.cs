using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetMeshColor : MonoBehaviour
{
    public Color color = Color.white;
    public bool randomColor = false;
    public bool setOnStart = true;
    public bool updateChildren = true;
    public bool updateSelf = true;

    // Start is called before the first frame update
    void Start()
    {
        if (setOnStart)
        {
           SetColor();
        }
    }

    [ContextMenu(nameof(SetColor))]
    private void SetColor()
    {
        if (randomColor)
        {
            color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
        }

        //  specific to Unity's URP
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color);

        //  self
        if (updateSelf)
        {
            MeshRenderer thisRend = GetComponent<MeshRenderer>();
            if (thisRend)
            {
                thisRend.SetPropertyBlock(properties);
            }
        }

        //  children
        if (updateChildren)
        {
            MeshRenderer[] childRenderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var childRend in childRenderers)
            {
                childRend.SetPropertyBlock(properties);
            }
        }
    }
}
