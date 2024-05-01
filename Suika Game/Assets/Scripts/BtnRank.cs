using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BtnRank : MonoBehaviour
{
    private void Start()
    {
        Button thisBtn = this.gameObject.GetComponent<Button>();
        thisBtn.onClick.AddListener(RankFunc);
    }

    public void RankFunc()
    {
        
    }
}
