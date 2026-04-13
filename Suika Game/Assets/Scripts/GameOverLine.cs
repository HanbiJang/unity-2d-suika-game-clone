using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameOverLine : MonoBehaviour
{
    /// <summary>
    /// true면 상대방 박스의 GameOverLine → 로컬 게임오버를 트리거하지 않음
    /// GameObjects_Other 하위의 GameOverLine에 체크
    /// </summary>
    [SerializeField] private bool isOtherPlayerLine = false;

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

    private float timer = 0f;
    private const float GAME_OVER_TIME = 2.0f; // 2 seconds grace period
    private List<GameObject> touchingFruits = new List<GameObject>();

    private void Update()
    {
        // Clean up null (destroyed) objects
        touchingFruits.RemoveAll(item => item == null);

        if (fruitManager == null)
        {
            // 멀티씬에서 FruitManager 이름이 다를 경우 재탐색
            fruitManager = GameObject.Find("FruitManager")?.GetComponent<FruitManager>();
            if (fruitManager == null) return;
        }

        if (touchingFruits.Count > 0 && fruitManager.isGameRun_func())
        {
            timer += Time.deltaTime;
            if (timer > 0 && timer < Time.deltaTime * 2) Debug.Log("GameOver Timer Started...");

            if (timer >= GAME_OVER_TIME)
            {
                if (!isOtherPlayerLine)
                    fruitManager.makeGameOver();
                Debug.Log("Game Over triggered by timer");
                timer = 0f;
                touchingFruits.Clear();
            }
        }
        else
        {
            timer = 0f;
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Fruit")
        {
            // Only count as touching if the fruit is NOT the one currently being held/dropped
            if (fruitManager.newFruitGameObject == null || collision.gameObject != fruitManager.newFruitGameObject)
            {
                if (!touchingFruits.Contains(collision.gameObject))
                {
                    touchingFruits.Add(collision.gameObject);
                    Debug.Log("Fruit touched GameOverLine: " + collision.gameObject.name);
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (touchingFruits.Contains(collision.gameObject))
        {
            touchingFruits.Remove(collision.gameObject);
        }
    }
}
