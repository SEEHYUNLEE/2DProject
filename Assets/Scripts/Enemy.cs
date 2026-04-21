using System.Diagnostics;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int maxHp = 100;
    int currentHp;

    void Start()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(int damage)
    {
        currentHp -= damage;

        if (currentHp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Destroy(gameObject);
    }
}
