using UnityEngine;
using Unity.Netcode;

public class GreatArcher : Archer
{
    void Awake()
    {
        damage = 25;
        attackRange = 12.0f;
    }
}
