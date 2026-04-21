using UnityEngine;

public class Warrior : MonoBehaviour
{
    public float moveSpeed = 4f;
    public int damage = 20;
    public float attackRange = 1.5f;
    public LayerMask enemyLayer;

    private bool isAttacking = false;
    private Collider2D currentEnemy;

    Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (isAttacking) return;

        currentEnemy = Physics2D.OverlapCircle(transform.position, attackRange, enemyLayer);

        if (currentEnemy != null)
        {
            StartAttack();
        }
        else
        {
            MoveForward();
        }
    }

    void MoveForward()
    {
        transform.Translate(Vector2.right * moveSpeed * Time.deltaTime);

        anim.SetBool("walk", true);
        //anim.SetTrigger("Attack"); // 혹시 남아있으면 초기화용
    }

    void StartAttack()
    {
        isAttacking = true;

        anim.SetBool("walk", false);
        anim.SetTrigger("attack"); // 트리거로 공격 시작
    }

    // 👉 애니메이션 이벤트에서 호출
    public void DealDamage()
    {
        if (currentEnemy != null)
        {
            Enemy enemyScript = currentEnemy.GetComponent<Enemy>();
            if (enemyScript != null)
            {
                enemyScript.TakeDamage(damage);
            }
        }
    }

    // 👉 애니메이션 끝에서 호출
    public void EndAttack()
    {
        isAttacking = false;
    }
}
