using System.Collections;
using UnityEngine;

public enum BossState
{
    Idle,
    Move,
    MeleeAttack,//近距離攻撃
    JumpAttack,
    RangedAttack,
    Wait
}
public class MidBoss1AI : MonoBehaviour
{
    //コンポーネント
    public Transform player;
    private Animator animator;
    private Rigidbody2D rb;
    GroundCheck gc;
    Player PlayerScript;


    //中ボスステータス
    public float moveSpeed = 2f;//移動速度
    public float attackRange = 2f;//近接攻撃範囲
    public float rangedAttackRange = 6f;//遠距離攻撃範囲
    public float jumpAttackTimer = 4f;//ジャンプ時間
    public float hoverSpeed = 3f;//プレイヤーのx方向に合わせるスピード
    public float fallSpeed = 8f;//落下速度
    [HideInInspector] public float MeleeAttackPercent;//近距離攻撃確率
    [HideInInspector] public float RangedAttackPercent;//遠距離攻撃確率
    private float RecordedMeleePercent;//今までの近距離攻撃確率保存


    //中ボスの行動管理//
    private BossState currentState = BossState.Idle;//最初はIdleの状態
    //左右移動
    private bool isFacingRight = true;//右向きかどうか
    //ジャンプ攻撃
    private bool isFlyng = false;//飛行中かどうか
    private bool isJumpAttacking = false;//ジャンプ攻撃中かどうか
    private bool isAscending = false;//上昇中かどうか
    //近距離攻撃
    private int maxMeleeCombo = 3;//近距離攻撃の最大連続数
    private bool MeleeCountThisFrame = false;//一度だけmeleeAttackCountを1増やす
    private bool MaxMelee = false;//maxMeleeCombo連続攻撃したかどうか
    private int meleeAttackCount = 0;//何連続近距離攻撃したか
    //クールタイム
    private float stateTimer = 0;//攻撃後のクールタイム
    //ダメージ受けた時
    private float blinkInterval = 0.1f;//点滅間隔
    private int blinkCount=3;//点滅回数

    //中ボスのアニメーション
    private int ViewCount = 1;//アニメーションを繰り返す回数
 

    //オブジェクト
    public GameObject Ground;
    public GameObject Seed;
    public GameObject AttackCollider;
    private GameObject Player;

    void Start()
    {
        Player = GameObject.Find("Player");
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        gc = Ground.GetComponent<GroundCheck>();
        PlayerScript = Player.GetComponent<Player>();
        MeleeAttackPercent = 0.80f;
        RangedAttackPercent = 0.15f;
    }
    void Update()
    {
        StateUpdate();
    }
    void StateUpdate()
    {
        float distance = Vector2.Distance(transform.position, player.position);
        switch (currentState)
        {
            case BossState.Idle:
                animator.Play("MB1Idle");
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0)
                {
                    if (distance < attackRange)//近距離攻撃範囲内なら近距離攻撃かジャンプ攻撃
                        ChangeState(Random.value < MeleeAttackPercent ? BossState.MeleeAttack : BossState.JumpAttack);
                    else if (distance < rangedAttackRange)//遠距離攻撃範囲内かつ近距離連続攻撃が3連続行われてなければ50%の確率で遠距離攻撃
                        ChangeState(Random.value < RangedAttackPercent && !MaxMelee ? BossState.RangedAttack : BossState.Move);
                    else//遠距離攻撃範囲にすら届いてなければ
                       ChangeState(BossState.Move);
                }
                break;

            case BossState.Move://左右移動
                animator.Play("MB1Move");
                MoveTowardsPlayer();
                if (distance < attackRange)//ここでも近距離攻撃範囲内なら近距離攻撃かジャンプ攻撃
                    ChangeState(Random.value < MeleeAttackPercent ? BossState.MeleeAttack : BossState.JumpAttack);
                break;

            case BossState.MeleeAttack://近距離攻撃
                animator.Play("MB1A1");

                if(!MeleeCountThisFrame)
                {
                    meleeAttackCount++;
                    if(meleeAttackCount == maxMeleeCombo)//近距離攻撃が最大連続回数に到達したら
                    {
                        RecordedMeleePercent = MeleeAttackPercent;//今までの近距離攻撃確率を保存
                        MeleeAttackPercent = 0;//近距離攻撃確率0%にし、次は必ずジャンプ攻撃
                        MaxMelee = true;
                    }
                    MeleeCountThisFrame = true;
                }
                rb.velocity = Vector2.zero; // 攻撃中は停止
                // 攻撃モーション終わったらクールタイム
                if (IsAnimationOver("MB1A1"))
                {
                    AttackCollider.SetActive(false);
                    ChangeState(BossState.Wait);
                }
                break;

            case BossState.RangedAttack:
                animator.Play("MB1A2");
                rb.velocity = Vector2.zero;
                ViewCount = 5;//アニメーション5回繰り返す
                meleeAttackCount = 0;//近距離攻撃連続回数0にする
                if (IsAnimationOver("MB1A2"))
                    ChangeState(BossState.Wait);
                break;

            case BossState.JumpAttack://ジャンプ攻撃
                rb.velocity = new Vector2(0, 0);
                meleeAttackCount = 0;//近距離攻撃連続回数0にする
                if (MaxMelee)
                {
                    MaxMelee = false;
                    MeleeAttackPercent = RecordedMeleePercent;//近距離攻撃確率を戻す
                }
                JumpAttackLogic();//ジャンプ攻撃演出
                break;

            case BossState.Wait://攻撃後のクールタイム
                ViewCount = 1;
                MeleeCountThisFrame = false;
                if (isJumpAttacking)
                {
                    animator.Play("MB1Drop2"); // 常に浮遊アニメーション再生
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        isJumpAttacking = false;
                        ChangeState(BossState.Idle); // Idleアニメにせず直接状態だけIdleへ
                    }
                }
                else
                {
                    animator.Play("MB1Idle"); // 通常の待機処理（ジャンプじゃない時）
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        ChangeState(BossState.Idle);
                    }
                }
                break;
        }
        if (currentState == BossState.Idle || currentState == BossState.Move)
            FlipSprite();
    }
    void ChangeState(BossState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case BossState.Idle:
                stateTimer = 0f; // Idleはタイマー不要
                break;
            case BossState.Wait:
                stateTimer = isJumpAttacking ? 2f : 1f; // Drop2中だけ2秒、他は短め
                break;
            default:
                stateTimer = 0.5f; // 汎用的なクールタイム
                break;
        }
    }
    void MoveTowardsPlayer()
    {
        float direction = player.position.x - transform.position.x;
        rb.velocity = new Vector2(Mathf.Sign(direction) * moveSpeed, rb.velocity.y);
    }
    void FlipSprite()
    {
        if ((player.position.x > transform.position.x && isFacingRight) ||
         (player.position.x < transform.position.x && !isFacingRight))
        {
            isFacingRight = !isFacingRight;
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }
    }
    bool IsAnimationOver(string animName)
    {
       
        return animator.GetCurrentAnimatorStateInfo(0).IsName(animName) &&
               animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= ViewCount;

    }
    void JumpAttackLogic()
    {
        //ジャンプして高速上昇
        if (!isJumpAttacking)
        {
            animator.Play("MB1Jump");
            isJumpAttacking = true;
        }
        AnimatorStateInfo animInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (animInfo.IsName("MB1Jump") && animInfo.normalizedTime >= 1.0f && !isAscending)
        {
            isAscending = true;
        }
        // 上昇中 Y=170 に向かって上がる
        if (isAscending && !isFlyng)
        {
            float targetY = 170f;
            float ascendSpeed = 20f;
            animator.Play("MB1Fly");
            Vector2 pos = transform.position;
            Vector2 targetPos = new Vector2(pos.x, targetY);
            transform.position = Vector2.MoveTowards(pos, targetPos, ascendSpeed * Time.deltaTime);

            // 到達したらMB1Flyに切り替え
            if (Mathf.Abs(transform.position.y - targetY) < 0.1f)
            {
                isAscending = false;
                isFlyng = true; // ここからは飛行フェーズ
            }
        }

        // 飛行中はX方向移動
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("MB1Fly") && isFlyng)
        {
            Vector2 targetPos = new Vector2(player.position.x, transform.position.y);
            transform.position = Vector2.MoveTowards(transform.position, targetPos, hoverSpeed * Time.deltaTime);
        }
        if (isFlyng)
        { jumpAttackTimer -= Time.deltaTime; }
        //落下
        if (isFlyng&&jumpAttackTimer <= 0f)
        {
            if (!gc.IsGrounded)
            {
                animator.Play("MB1Drop1");
                rb.velocity = new Vector2(0, -fallSpeed);
            }
            else
            {
                rb.velocity = Vector2.zero;
                ChangeState(BossState.Wait);
                isFlyng = false;
                jumpAttackTimer = 4f;
            }
        }
    }
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerScript.Damage();//プレイヤーにダメージを与える
        }
        if (collision.CompareTag("rob"))
        {
            StartCoroutine(FlashOnHit());
        }
    }
    void AttackSetactiveTrue()//近接攻撃時そのコライダーオン
    {
        AttackCollider.SetActive(true);
    }
    void SeedAttack()//タネ飛ばし攻撃でタネを出す
    {
        float PlusY=0;
        float direction = isFacingRight ? -1f : 1f;// 中ボスの向きと同じ向きにタネを飛ばす
        PlusY = -0.5f;//飛ばすタネの高さ
        Vector2 StartPosition = new Vector2(transform.position.x + direction, transform.position.y + PlusY);//飛ばす位置
        GameObject seed=Instantiate(Seed, StartPosition, Quaternion.identity);
        Rigidbody2D rb = seed.GetComponent<Rigidbody2D>();
        float seedSpeed = 15f;//タネを飛ばすスピード

        rb.velocity = new Vector2(seedSpeed * direction, 0);//タネを飛ばす

        // 種の見た目の向きも調整
        if (!isFacingRight)
        {
            Vector3 scale = seed.transform.localScale;
            scale.x *= -1;
            seed.transform.localScale = scale;
        }
    }
    //ダメージ受けたら点滅
    IEnumerator FlashOnHit()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        for (int i = 0; i < blinkCount; i++)
        {
            sr.color = new Color(1, 1, 1, 0.3f); // 半透明にさせる
            yield return new WaitForSeconds(blinkInterval);
            sr.color = Color.white; // 通常色のもどす
            yield return new WaitForSeconds(blinkInterval);
        }
    }

}
