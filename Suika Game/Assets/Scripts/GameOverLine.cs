using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameOverLine : MonoBehaviour
{
    FruitManager fruitManager;

    private void Awake()
    {
        try
        {
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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Fruit" && fruitManager.isGameRun_func() && fruitManager.isReady)
        {
            fruitManager.makeGameOver();
            Debug.Log("Game Over");
        }
    }
}
