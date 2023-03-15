using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameData
{
    public Vector3 playerPosition;
    public GameDifficulty gd;
    public int playerHealth;
    public enum GameDifficulty
    {
        Easy,
        Normal
    }
    public GameData()
    {
        playerPosition = new Vector3();
        gd = GameDifficulty.Easy;
        playerHealth = 2;
    }

    public void ChangeDifficilty(GameDifficulty difficulty)
    {
        if (gd == GameDifficulty.Normal)
        {
            playerHealth = 1;
        }
        gd = difficulty;
    }

    public bool GetDamage(int damage)
    {
        playerHealth -= damage;
        if (playerHealth <= 0)
        {
            playerHealth = 0;
            return true; // player is dead
        }
        else return false; // player is alive
    }
}
