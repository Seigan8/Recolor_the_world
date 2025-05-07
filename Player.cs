using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    //コンポーネント
    private Rigidbody2D Player_rb;
    private Animator animator;
    private CapsuleCollider2D Capsule;
    private EdgeCollider2D Edge;
    private BoxCollider2D Box;
    private SpriteRenderer Sprite;
    private GroundCheck gc;//設置判定スクリプト

    //入力・物理関連
    private float currentVelocity;//HorizontalInput * move_speed;
    private float HorizontalInput;//左右の入力を数値化
    //private Vector2 AttackPosition;
    private Vector2 Direction;//目の前に壁があるか確かめるためのRaycastHit2Dの方向
    public GameObject[] attack_collider;//攻撃用コライダーのオブジェクト

    //プレイヤー状態
    private int Element = 2;//ハートオブジェクトの要素
    private int Sprite_Element = 2;//ハートのpngの要素
    [HideInInspector] public bool isDead = false;//連続で死なないように
    [HideInInspector] public bool isOnIce = false;
    private bool isJumping = false;//ジャンプ中かどうか
    private bool isAttacking = false;//攻撃中かどうか
    private bool facingRight = true;//右に向いてるかどうか
    private bool isInvicible = false;//無敵時間中かどうか

    //プレイヤーのアニメーション関連
    private float jumpStartTime;//再生されてからジャンプするまでの時間
    private float minJumpTime = 0.05f;//ジャンプの最低時間

    //プレイヤーのパラメータ
    private int LifePoint = 6;//プレイヤーのHP
    public int Player_Attack;//攻撃力
    public float move_speed;//通常ダッシュ速度
    public float dash_speed;//さらにダッシュ速度
    private float jump_force; //ジャンプ力
    public float Big_jump_force;//大ジャンプ力
    public float Small_jump_force;//小ジャンプ力
    public float Big_Jump_MiniTime;//大ジャンプの最小の長押し時間
    public float attacking_time;//攻撃時間
    private float Jump_Press_Time;
    private float maxIceSpeed = 12;//氷床での最大速度
    public float iceAcceleration;//氷床での加速度

    //無敵時間
    private float blinkDuration = 1.5f;//無敵時間
    private float blinkInterval = 0.1f;//点滅間隔
    private float elapsedTime = 0f; //blinkIntervalごとに点滅感覚を足す

    //UI関連
    public GameObject[] LifeHeart;//HPのUI
    public Sprite[] Life_Sprite;//HPのスプライト
    public GameObject Slider;//パレットオブジェクト

    //その他
    public LayerMask GroundLayer;//ステージのレイヤー
    public string Attacking_name;//攻撃の種類の名前

    void Start()
    {
        Player_rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        Capsule = GetComponent<CapsuleCollider2D>();
        Edge = GetComponent<EdgeCollider2D>();
        Box = GetComponent<BoxCollider2D>();
        Sprite = GetComponent<SpriteRenderer>();
        gc = GameObject.Find("GroundCheck").GetComponent<GroundCheck>();
    }

    void Update()
    {
        HorizontalInput = Input.GetAxisRaw("Horizontal");

        if (!isAttacking && !isDead)//攻撃してない時とと死んでない時は他のアニメーションが可能
        {
             //進む方向にスプライトと攻撃時のオブジェクト左右反転
            if (!isJumping) { Idle();  }
            Invert();
            Attack();
            Jump();
            Dash();
            
        }
        if (isOnIce) OnIce();//氷に乗った際の慣性処理
        else currentVelocity = HorizontalInput * move_speed;
        //if (isAttacking) currentVelocity=0;
        AttackAnimations();
     
    }
    private void Invert()
    {
        if (Input.GetAxisRaw("Horizontal") > 0 && !facingRight|| Input.GetAxisRaw("Horizontal") < 0 && facingRight)
        {
            FlipColliderObject();
        }
    }
    private void Idle()
    {
        {
            if (Input.GetButtonUp("Horizontal"))//地面に接触していてHorizontalKeyを離したら止まるアニメーション
            {
                animator.CrossFade("Player_Idle", 0.5f);
            }
        }
    }
    private void Dash()
    {
        if(!CheckWall())
        {
            //通常ダッシュ
            Player_rb.velocity = new Vector2(currentVelocity, Player_rb.velocity.y);
            if(HorizontalInput != 0&& !isJumping)//ジャンプ中は走るアニメーションをしない
            {
                animator.Play("Player_Run");//走るアニメーションをする
            }
        }
    }
    private void Jump()
    {
        if(!isJumping)//空中ジャンプを防ぐ
        {
            if (Input.GetButtonDown("Jump"))
            {
                Jump_Press_Time = Time.time;//再生されてからの時間ジャンプキー押すまでの時間
            }
            if (Input.GetButtonUp("Jump") )//キーを離したらジャンプ
            {
                isJumping = true;
                jumpStartTime = Time.time;
                float heldTime = Time.time - Jump_Press_Time;
                //どれくらい長押ししたかで小ジャンか大ジャンを決める
                jump_force = heldTime < Big_Jump_MiniTime ? Small_jump_force : Big_jump_force;
                //y軸に力を与える
                Player_rb.velocity = new Vector2(currentVelocity, jump_force);
                animator.Play("Player_Jump");//ジャンプアニメーションをする
            }
        }
        if (isJumping && gc.IsGrounded&&(Time.time - jumpStartTime > minJumpTime)) // ジャンプした後着地したと判断、念の為isGrounded()も入れてる
        {//(Time.time - jumpStartTime > minJumpTime)で飛んだ瞬間にすぐ他のアニメーションに上書きされるのを防ぐ
            animator.Play("Player_Idle");//ジャンプした後止まるアニメーションにする
            isJumping = false;
        }
    }
    private void Attack()//入力キーによって攻撃パターンを変える
    {
        if (Input.GetKeyDown(KeyCode.J)&& !CheckWall())
        {
            //目の前に壁がなければ攻撃アクション開始
            Slider.GetComponent<Slide_Move>().Painting();
        }
    }
    public void Attacking_time()//攻撃アニメーション
    {
        if (isAttacking) return;
        isAttacking = true;
        if (!isOnIce) currentVelocity = 0;//攻撃中は強制停止
        animator.Play("P_Attack");
        switch(Attacking_name)//攻撃の種類によってアニメーションを変える
        {
            case "Normal": animator.SetInteger("state", 0); break;
            case "Fire": animator.SetInteger("state", 1); break;
            case "Heal":
                animator.SetInteger("state", 2);
                if (LifePoint < 6) Healing();
                break;
            case "Water": animator.SetInteger("state", 3); break;
            case "Ice": animator.SetInteger("state", 4); break;
            case "Thunder": animator.SetInteger("state", 5); break;
            case "Poison": animator.SetInteger("state", 6); break;
        }
        CancelInvoke(nameof(End_Attack));
        Invoke(nameof(End_Attack), attacking_time);//attacking_timeの時間だけ isAttacking = true;
    }
    private void End_Attack()//攻撃終了
    {
        isAttacking = false;
        attack_collider[1].SetActive(false);//攻撃のアニメーション時限定の新たな当たり判定off
        Capsule.enabled = true;//プレイヤーの通常コライダーオン
    }
    private void AttackAnimations()
    {
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("P_Attack"))//アニメーションP_Attack1の時は通常のコライダー
        {
            attack_collider[0].SetActive(true);//攻撃の構えのアニメーション時限定の新たな当たり判定off
        }
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("P_Attack2"))//アニメーションP_Attack1の時は通常のコライダー
        {
            Capsule.enabled = false;//プレイヤーの通常コライダーオフ
        }
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("P_Normal_A")
            || animator.GetCurrentAnimatorStateInfo(0).IsName("P_Fire_A")
            || animator.GetCurrentAnimatorStateInfo(0).IsName("P_Heal")
            || animator.GetCurrentAnimatorStateInfo(0).IsName("P_Water_A")
            || animator.GetCurrentAnimatorStateInfo(0).IsName("P_Thunder_A")
            || animator.GetCurrentAnimatorStateInfo(0).IsName("P_Ice_A")
            )
        {
            attack_collider[0].SetActive(false);//構えの判定off
            attack_collider[1].SetActive(true);//攻撃のアニメーション時限定の新たな当たり判定on
        }
    }
    private void FlipColliderObject()//攻撃コライダーごと反転
    {
        facingRight = !facingRight;
        Sprite.flipX = !Sprite.flipX;

        attack_collider[0].transform.localScale = new Vector2(-attack_collider[0].transform.localScale.x,
              attack_collider[0].transform.localScale.y);
        attack_collider[1].transform.localScale = new Vector2(-attack_collider[1].transform.localScale.x,
                attack_collider[1].transform.localScale.y);
    }
    public void Damage()//ダメージをくらうごとにハートが削られる演出 三段階の状態のハート三個　LifePoint 0から6の7段階
    {
        if (isInvicible) return;//無敵時間じゃないならくらう
        LifePoint--;

        if (LifePoint >= 4) { Element = 2; }//一番右のハートを取得
        else if (LifePoint >= 2) { Element = 1; }//真ん中のハートを取得
        else if (LifePoint >= 0) { Element = 0; }//一番左のハートを取得
        if (LifePoint % 2 == 1) { Sprite_Element = 1; }//ハート半分のスプライト取得
        else if (LifePoint % 2 == 0) { Sprite_Element = 2; }//ハートゼロのスプライト取得
        LifeHeart[Element].GetComponent<Image>().sprite = Life_Sprite[Sprite_Element];

        if (LifePoint == 0)  Die(); 
        else StartCoroutine(BlinkCoroutine());//敵に当たったら一定時間点滅し、無敵になる
    }

    IEnumerator BlinkCoroutine()//無敵時間演出
    {
        isInvicible = true;
        elapsedTime = 0;

        while (elapsedTime < blinkDuration)
        {
            Sprite.enabled = !Sprite.enabled;
            yield return new WaitForSeconds(blinkInterval);
            elapsedTime += blinkInterval;
        }
        Sprite.enabled = true;//スプライトオフデア割る可能性もあるので最後に必ずスプライトオン
        isInvicible = false;
    }
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        animator.Play("P_Die");
        Player_rb.velocity = Vector2.zero;//動きを止める
        Player_rb.simulated = false;//物理挙動を無効にする
        
    }
    private void Healing()//回復攻撃
    {
        LifePoint++;
        if (LifePoint >= 5) { Element = 2; }//一番右のハートを取得
        else if (LifePoint >= 3) { Element = 1; }//真ん中のハートを取得
        else if (LifePoint >= 1) { Element = 0; }//一番左のハートを取得

        if (LifePoint % 2 == 1) { Sprite_Element = 1; }//ハート半分のスプライト取得
        else if (LifePoint % 2 == 0) { Sprite_Element = 0; }//ハートゼロのスプライト取得
        LifeHeart[Element].GetComponent<Image>().sprite = Life_Sprite[Sprite_Element];
    }
    private bool CheckWall()//目の前に壁がなければ攻撃可能
    {
        Direction = facingRight ? Vector2.right : Vector2.left;//方向を決める
       //進む方向に当たり判定作る
        RaycastHit2D hit = Physics2D.Raycast(this.transform.position, Direction, 1f, GroundLayer);
        return hit.collider != null;
    }
    private void OnTriggerStay2D(Collider2D other)
    {//子オブジェクトの攻撃コライダー用のオブジェクトでもやられるアニメーションが起きてしまうため、
        if (other.CompareTag("Throns") && !isInvicible)
        {
            Damage();
        }
    }
    private void A_Collider_On()
    {
        Edge.enabled = false;
    }
    void OnIce()
    {
        float maxSpeed = isOnIce ? maxIceSpeed : move_speed;
        if ( HorizontalInput != 0&&!isAttacking)//攻撃してなく、左右キー押してたら氷床は加速
        {
            currentVelocity += iceAcceleration * Time.deltaTime * Mathf.Sign(HorizontalInput);
            currentVelocity = Mathf.Clamp(currentVelocity, -maxSpeed, maxSpeed);
        }
        else//徐々に減速（慣性）
        {
            currentVelocity = Mathf.MoveTowards(currentVelocity, 0f, iceAcceleration * Time.deltaTime);
        }
    }

}




