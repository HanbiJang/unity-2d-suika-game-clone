using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CursorControl : MonoBehaviour
{
    Vector3 target;
    public Vector3 cursorPos;
    FruitManager fruitManager;

    private void Start()
    {
        fruitManager = GameObject.Find("FruitManager").GetComponent<FruitManager>();
    }

    // Update is called once per frame
    void Update()
    {
        target = new Vector3(Camera.main.ScreenToWorldPoint(Input.mousePosition).x, 0, 0);

        if(target.x >= fruitManager.leftBorder && target.x <= fruitManager.rightBorder)
        {
            this.gameObject.transform.position = target;       
        }
        cursorPos = this.gameObject.transform.position;
    }
}
