using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundControl : MonoBehaviour
{
    public bool isPlayed = false;
    FruitManager fruitManager;

    private void Awake()
    {
        try{
            fruitManager = GameObject.Find("FruitManager").GetComponent<FruitManager>();
        }
        catch
        {
            if (!fruitManager)
            {
                Debug.LogError("Please create a FruitManager in GameScene");
            }
        }
        

    }

    private void OnCollisionEnter2D(Collision2D collision)
    {

        if (collision.gameObject == fruitManager.newFruitGameObject && !isPlayed)
        {
            isPlayed = true;
            fruitManager.EffectSoundPlay(EffectSound.Drop);
        }

    }
}